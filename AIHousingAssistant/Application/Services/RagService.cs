using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

using AIHousingAssistant.Models.Settings;
using Microsoft.Extensions.Options;
using AIHousingAssistant.Helper;
using OllamaSharp;
using AIHousingAssistant.Application.Services.Interfaces;
using AIHousingAssistant.Application.Enum;
using AIHousingAssistant.Application.Services.VectorStores;
using AIHousingAssistant.Models;
using AIHousingAssistant.Application.Services.Chunk;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.AspNetCore.Http.HttpResults;
using System.Collections.Concurrent;

namespace AIHousingAssistant.Application.Services
{
    public class RagService : IRagService
    {
        private readonly ProviderSettings _providerSettings;
        private readonly IChunkService _chunkService;
        private readonly OllamaApiClient _ollamaClient;
        private readonly IVectorStore _vectorStore;
        private readonly Kernel _kernel;
        private readonly IChatHistoryService _historyService;
        // NEW: Use resolver instead of injecting 3 stores

        public RagService(
            IOptions<ProviderSettings> providerSettings,
            IChunkService chunkService,
            IVectorStore vectorStore,
            IChatHistoryService historyService,
            Kernel kernel
            )
        {
            if (providerSettings == null)
                throw new ArgumentNullException(nameof(providerSettings));

            _providerSettings = providerSettings.Value;
            _chunkService = chunkService ?? throw new ArgumentNullException(nameof(chunkService));
            _vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
            // Initialize Ollama client for answer generation (llama3)
            _ollamaClient = new OllamaApiClient(new Uri(_providerSettings.Ollama.Endpoint));
            _ollamaClient.SelectedModel = _providerSettings.Ollama.Model;
            _historyService = historyService ?? throw new ArgumentNullException(nameof(historyService));
            _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));

        }

        // --------------------------------------------
        // Process uploaded document and store vectors using selected provider
        public async Task ProcessDocumentAsync(
            IFormFile file,
           RagUiRequest ragUiRequest
            )
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("File is empty", nameof(file));



            // 1) Save locally
            var filePath = await FileHelper.SaveFileAsync(file, _providerSettings.ProcessingFolder);
            var source = FileHelper.GetSafeFileNameFromPath(filePath);

            // 2) Extract
            var textExtracted = await FileHelper.ExtractDocumentAsync(filePath, source);


            // 3) Chunk
            var chunks = await _chunkService.CreateChunksAsync(textExtracted, ragUiRequest, source);

            if (chunks == null || chunks.Count == 0)
                throw new InvalidOperationException("No text chunks were generated from the document.");

            // 4) Store vectors in the selected vector store
            //var store = _vectorStoreResolver.Resolve(ragUiRequest.VectorStoreProvider);
            await _vectorStore.StoreTextChunksAsVectorsAsync(chunks, ragUiRequest);
        }


        // --------------------------------------------
        // New unified method
        public async Task<RagAnswerResponse> AskRagAsync(RagUiRequest ragRequest)
        {
            // Validate the incoming query
            if (string.IsNullOrWhiteSpace(ragRequest.Query))
                return new RagAnswerResponse { Answer = "Query is empty." };


            // 1. Retrieval (Vector Retrieval)
            // Use a switch expression to dynamically select the search strategy (Hybrid, Semantic, or Pure Vector)
            // The full ragRequest object is passed to IVectorStore to enable dynamic provider selection 
            // and retrieval limit (TopSimilarity).
            // ragRequest.TopSimilarity = 3;
            List<VectorChunk>? chunks = ragRequest.SearchMode switch
            {
                // Hybrid Search: Combines semantic search with keyword filtering.
                SearchMode.Hybrid => await _vectorStore.HybridSearchAsync(ragRequest.Query, ragRequest),

                // Semantic Search: Finds closest matches based on embedding similarity.
                SearchMode.Semantic => await _vectorStore.SemanticSearchAsync(ragRequest.Query, ragRequest),

                // Pure Vector Search (Default for basic requests): Finds the single closest match.
                // The single VectorChunk? result is wrapped in a List<VectorChunk>.
                SearchMode.Vector => (await _vectorStore.VectorSearchAsync(ragRequest.Query, ragRequest)) is { } singleChunk
                                     ? new List<VectorChunk> { singleChunk }
                                     : new List<VectorChunk>(),

                // Default Case: Fallback to the basic Vector Search if the mode is unspecified or unknown.
                _ => (await _vectorStore.VectorSearchAsync(ragRequest.Query, ragRequest)) is { } defaultChunk
                     ? new List<VectorChunk> { defaultChunk }
                     : new List<VectorChunk>()
            };

            // Check if the retrieval step returned any results
            if (chunks == null || chunks.Count == 0)
                return new RagAnswerResponse { Answer = "No related answer found." };

            // Filter out chunks that might have null or empty content (cleanup)
            var usedChunks = chunks
                .Where(c => !string.IsNullOrWhiteSpace(c.Content))
                .ToList();

            if (usedChunks.Count == 0)
                return new RagAnswerResponse { Answer = "No related answer found." };

            // 2. Generation (Answer Synthesis)
            // Combine the content of all retrieved chunks into a single context string for the LLM.
            var context = string.Join("\n\n---\n\n", usedChunks.Select(c => c.Content));
            var answer = await ExtractAnswerFromChunkAsync(ragRequest, context);

            if (string.IsNullOrWhiteSpace(answer))
                answer = "No related answer found.";

            // 3. Return Response (Packaging Results)
            return new RagAnswerResponse
            {
                Answer = answer,
                ChunkIndexes = usedChunks.Select(c => c.Index).Distinct().ToList(),
                Sources = usedChunks.Select(c => c.Source).Distinct().ToList(),
                Similarity = usedChunks.Select(c => c.Similarity).ToList()
            };
        }




        private async Task<string> ExtractAnswerFromChunkAsync(RagUiRequest ragRequest, string chunkContent)
        {
            // 1️⃣ Validate inputs: return empty if no chunkContent
            if (string.IsNullOrWhiteSpace(chunkContent))
                return string.Empty;

            // 2️⃣ Return chunkContent directly if no user query
            if (string.IsNullOrWhiteSpace(ragRequest.Query))
                return chunkContent.Trim();

            // 3️⃣ Resolve the Chat Completion Service from Semantic Kernel
            var chatService = _kernel.GetRequiredService<IChatCompletionService>();

            // 4️⃣ Retrieve persistent history for this session
            var persistentHistory = _historyService.GetOrCreateHistory(ragRequest.SessionId);

            // 5️⃣ Create a temporary ChatHistory for this execution
            var temporaryHistory = new ChatHistory();

            // 6️⃣ Add system instructions to guide AI behavior
            temporaryHistory.AddSystemMessage(@"
You are an AI assistant.
Answer the user's question based on the provided CONTEXT or the previous conversation HISTORY.
- For housing-related questions, use ONLY the information in the CONTEXT.
- If the answer is not in the CONTEXT or HISTORY, say: 'I don't know based on the provided information.'
- Keep answers short and direct (one or two sentences).
- If the user asks about their previous questions, provide a list of those questions.
- Do NOT mention the words CONTEXT or HISTORY in your answer.");

            // 7️⃣ Prepare a textual representation of all previous user questions
            var previousUserQuestions = string.Join("\n", persistentHistory
                .Where(m => !string.IsNullOrWhiteSpace(m.Content))
                .Select(m => m.Content));

            // 8️⃣ Add previous user questions and current chunk + query to temporary history
            temporaryHistory.AddUserMessage($@"
PREVIOUS USER QUESTIONS:
{previousUserQuestions}

CONTEXT:
{chunkContent}

NEW QUESTION:
{ragRequest.Query}");

            // 9️⃣ Call the AI model to generate the response
            var result = await chatService.GetChatMessageContentAsync(temporaryHistory, kernel: _kernel);
            string assistantAnswer = result.ToString().Trim();

            // 10️⃣ Save only the current user query and assistant answer to persistent history
            _historyService.AddUserMessage(ragRequest.SessionId, ragRequest.Query);
            _historyService.AddAssistantMessage(ragRequest.SessionId, assistantAnswer);

            // 11️⃣ Return assistant's answer
            return assistantAnswer;
        }

    }
}
        // --------------------------------------------


        // --------------------------------------------

//        private async Task<string> ExtractAnswerFromChunkAsync(RagUiRequest ragRequest, string chunkContent)
//        {
//            // Validate inputs: if no context is provided, we can't answer.
//            if (string.IsNullOrWhiteSpace(chunkContent))
//                return string.Empty;

//            // If there is no query, just return the raw content (fallback).
//            if (string.IsNullOrWhiteSpace(ragRequest.Query))
//                return chunkContent.Trim();

//            // 1. Resolve the Chat Completion Service from the Semantic Kernel
//            var chatService = _kernel.GetRequiredService<IChatCompletionService>();


//            // 2. Retrieve the PERSISTENT history for this specific session.
//            // This history only contains clean previous user questions and AI answers.
//            var persistentHistory = _historyService.GetOrCreateHistory(ragRequest.SessionId);

//            // 3. Create a TEMPORARY ChatHistory for this specific execution.
//            // This ensures that bulky instructions and context chunks don't bloat our long-term memory.
//            var temporaryHistory = new ChatHistory();

//            // 4. Add the System Instructions to the temporary history.
//            // These constraints guide the AI's behavior for this specific request.
//            temporaryHistory.AddSystemMessage(@"
//        You are an AI assistant.
//        Answer the user's question based on the provided CONTEXT or the previous conversation HISTORY.
//        - If the user asks about previous messages, refer to the conversation history.
//        - For housing-related questions, use ONLY the information in the CONTEXT.
//        - If the answer is not in the CONTEXT or HISTORY, say: 'I don't know based on the provided information.'
//        - Keep answers short and direct (one or two sentences).
//        - Do NOT mention the words CONTEXT or HISTORY in your answer.");

//            // 5. Append previous clean Q&A from the persistent history to the temporary conversation.
//            foreach (var message in persistentHistory)
//            {
//                temporaryHistory.Add(message);
//            }

//            // 6. Add the CURRENT Context and the NEW Query to the temporary history.
//            // We use a verbatim string to ensure clear separation between Context and Question.
//            temporaryHistory.AddUserMessage($@"
//        CONTEXT:
//        {chunkContent}

//        QUESTION:
//        {ragRequest.Query}");

//            // 7. Call the AI model (Ollama) to generate the response based on the combined history.
//            var result = await chatService.GetChatMessageContentAsync(temporaryHistory, kernel: _kernel);
//            string assistantAnswer = result.ToString().Trim();

//            // 8. SAVE ONLY the original query and the clean assistant answer to the persistent service.
//            // This keeps our long-term session history lightweight and efficient.
//            _historyService.AddUserMessage(ragRequest.SessionId, ragRequest.Query);
//            _historyService.AddAssistantMessage(ragRequest.SessionId, assistantAnswer);

//            return assistantAnswer;
//        }

//    }
//}