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
using AIHousingAssistant.Application.SemanticKernel;
using AIHousingAssistant.semantic.Plugins;
using Microsoft.SemanticKernel.Connectors.OpenAI;

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

            string? answer = ragRequest.RagModel switch
            {

                RagModel.Ollama => await ExtractAnswerFromChunkByOllamaAsync(ragRequest, context),

                RagModel.OpenAI => await ExtractAnswerFromChunkByOpenAIAsync(ragRequest, context)
            };



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




        private async Task<string> ExtractAnswerFromChunkByOllamaAsync(RagUiRequest ragRequest, string chunkContent)
        {
            // 1️⃣ Validate inputs
            if (string.IsNullOrWhiteSpace(chunkContent))
                return string.Empty;

            if (string.IsNullOrWhiteSpace(ragRequest.Query))
                return chunkContent.Trim();

            // 2️⃣ Resolve Chat Completion Service
            var chatService = _kernel.GetRequiredService<IChatCompletionService>();

            // 3️⃣ Retrieve persistent history
            var persistentHistory = _historyService.GetOrCreateHistory(ragRequest.SessionId);

            // 4️⃣ Create temporary ChatHistory
            var temporaryHistory = new ChatHistory();

            // 5️⃣ Add system instructions
            temporaryHistory.AddSystemMessage(@"
You are an AI assistant.
Answer the user's question based on the provided CONTEXT or the previous conversation HISTORY.
- For housing-related questions, use ONLY the information in the CONTEXT.
- If the answer is not in the CONTEXT or HISTORY, say: 'I don't know based on the provided information.'
- Keep answers short and direct (one or two sentences).
- If the user asks about their previous questions, provide a list of those questions.
- Do NOT mention the words CONTEXT or HISTORY in your answer.");

            // 6️⃣ Prepare previous user questions
            string previousQuestionsText = string.Join("\n", persistentHistory
                .Where(m => m.Role == AuthorRole.User && !string.IsNullOrWhiteSpace(m.Content))
                .Select((m, i) => $"{i + 1}. {m.Content}"));

            // 7️⃣ Add previous questions + current chunk + new query
            temporaryHistory.AddUserMessage($@"
PREVIOUS USER QUESTIONS:
{previousQuestionsText}

CONTEXT:
{chunkContent}

NEW QUESTION:
{ragRequest.Query}");

            // 8️⃣ Call AI model
            var result = await chatService.GetChatMessageContentAsync(temporaryHistory, kernel: _kernel);
            string assistantAnswer = result.ToString().Trim();

            // 9️⃣ Save current query and assistant answer
            _historyService.AddUserMessage(ragRequest.SessionId, ragRequest.Query);
            _historyService.AddAssistantMessage(ragRequest.SessionId, assistantAnswer);

            // 🔟 Return answer
            return assistantAnswer;
        }


        
            // Declare questionHistory as a class-level variable to store all previous questions
            private List<string> questionHistory = new List<string>();

            private async Task<string> ExtractAnswerFromChunkByOpenAIAsync(
                RagUiRequest ragRequest,
                string chunkContent)
            {
                // 1️⃣ Validate the user query to ensure it's not empty or whitespace
                if (string.IsNullOrWhiteSpace(ragRequest.Query))
                {
                    throw new ArgumentException("Query cannot be empty.");
                }

                // If the user's query is asking for the previous question, return it
                if (ragRequest.Query.Equals("What is the previous question?", StringComparison.OrdinalIgnoreCase))
                {
                    if (questionHistory.Any())
                    {
                        return questionHistory.LastOrDefault() ?? "No previous question found.";
                    }
                    else
                    {
                        return "No previous question found."; // No question history yet
                    }
                }

                try
                {
                    // ---------------------------
                    // 2️⃣ Build the Kernel for OpenRouter
                    // ---------------------------
                    var kernelBuilder = SemanticKernelHelper.BuildKernel(AIProvider.OpenRouter, _providerSettings);
                    var kernel = SemanticKernelHelper.Build(kernelBuilder);

                    // ---------------------------
                    // 3️⃣ Get ChatCompletionService from the Kernel
                    // ---------------------------
                    var chatService = kernel.GetRequiredService<Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService>();

                    // ---------------------------
                    // 4️⃣ Prepare chat history and system/user messages
                    // ---------------------------
                    var chatHistory = new Microsoft.SemanticKernel.ChatCompletion.ChatHistory();

                    // System message: define AI behavior
                    chatHistory.AddSystemMessage("You are a helpful AI assistant.");

                    // User message: question + document chunk
                    chatHistory.AddUserMessage($@"
The user has asked: ""{ragRequest.Query}"". 
Use the document chunk provided below to generate a clear and concise answer that directly addresses the user's question. Ensure that all relevant details from the document are included in your response:

{chunkContent}
");

                    // Add the user's query to the question history
                    questionHistory.Add(ragRequest.Query);

                    // ---------------------------
                    // 5️⃣ Execution settings
                    // ---------------------------
                    Microsoft.SemanticKernel.Connectors.OpenAI.OpenAIPromptExecutionSettings? executionSettings = null;

                    // ---------------------------
                    // 6️⃣ Call OpenRouter AI
                    // ---------------------------
                    var response = await chatService.GetChatMessageContentAsync(chatHistory, executionSettings, kernel);

                    // Trim and clean up the response content, if it exists
                    string botResponse = response.Content?.Trim() ?? string.Empty;

                    // ---------------------------
                    // 7️⃣ Return the AI answer
                    // ---------------------------
                    return botResponse;
                }
                catch (Exception ex)
                {
                    // Log or handle the exception if necessary, preserving the original stack trace
                    throw new ApplicationException("An error occurred while processing the query with OpenRouter AI.", ex);
                }
            }
        }

    }



    



     