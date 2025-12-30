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
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI.OpenAI;
using AIHousingAssistant.Application.Services.ChatTools.Interfaces;
using AIHousingAssistant.Application.Services.DocumentProcessing.Abstractions;
namespace AIHousingAssistant.Application.Services.ChatTools
{
    public class RagService : IRagService
    {
        private readonly Settings _providerSettings;
        private readonly IChunkService _chunkService;
        private readonly OllamaApiClient _ollamaClient;
        private readonly IVectorStore _vectorStore;
        private readonly Kernel _kernel;
        private readonly IChatHistoryService _historyService;
        private readonly IDocProcessor _docProcessor; 
        // NEW: Use resolver instead of injecting 3 stores

        public RagService(
            IOptions<Settings> providerSettings,
            IChunkService chunkService,
            IVectorStore vectorStore,
            IChatHistoryService historyService,
            Kernel kernel,
            IDocProcessor docProcessor
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
            _docProcessor = docProcessor ?? throw new ArgumentNullException(nameof(docProcessor));
        }

        // --------------------------------------------
        //  Process uploaded document and store vectors using selected provider
        public async Task ProcessDocumentByRagAsync(List<IFormFile> files, RagUiRequest ragUiRequest)
        {
            // Check if the list is null or empty
            if (files == null || files.Count == 0)
                throw new ArgumentException("No files provided for processing.");

            // Loop through each file in the list
            foreach (var file in files)
            {
                // Skip empty files
                if (file == null || file.Length == 0)
                    continue;

                try
                {
                    //// 1) Save the file locally to the processing folder
                    //var filePath = await FileHelper.SaveFileAsync(file, _providerSettings.ProcessingFolder);
                    //var source = FileHelper.GetSafeFileNameFromPath(filePath);

                    //// 2) Extract text from the saved document
                    //// var textExtracted = await FileHelper.ExtractDocumentAsync(filePath, source);
                    // This will: Save -> Convert (MarkItDown) -> MetaData (Language) -> Normalize
                    string markdownFilePath = await _docProcessor.ProcessAndSaveAsync(file);

                    // Get safe name for source tracking
                    var source = Path.GetFileName(markdownFilePath);

                    // Read the clean markdown content to be chunked
                    var cleanText = await File.ReadAllTextAsync(markdownFilePath);



                    // 3) Split text into chunks based on the selected RagUiRequest configuration
                    var chunks = await _chunkService.CreateChunksAsync(cleanText, ragUiRequest, source);

                    if (chunks == null || chunks.Count == 0)
                    {
                        // Log or handle files that produce no content
                        continue;
                    }

                    // 4) Store generated vectors in the selected Vector Database
                    await _vectorStore.StoreTextChunksAsVectorsAsync(chunks, ragUiRequest);
                }
                catch (Exception ex)
                {
                    // You can log the error for a specific file here and continue with others
                    // _logger.LogError($"Error processing file {file.FileName}: {ex.Message}");
                    throw; // Or rethrow if you want to stop the entire process
                }
            }
        }

        // ----------------------------------s----------
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
                SearchMode.Vector => await _vectorStore.VectorSearchAsync(ragRequest.Query, ragRequest) is { } singleChunk
                                     ? new List<VectorChunk> { singleChunk }
                                     : new List<VectorChunk>(),

                // Default Case: Fallback to the basic Vector Search if the mode is unspecified or unknown.
                _ => await _vectorStore.VectorSearchAsync(ragRequest.Query, ragRequest) is { } defaultChunk
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
            string? answer =await ExtractAnswerFromChunkByChatModelAsync(ragRequest, context);
            //string? answer = ragRequest.AIProvider switch
            //{

            //    AIProvider.Ollama => await ExtractAnswerFromChunkByOllamaAsync(ragRequest, context),
            //    AIProvider.Groq => await ExtractAnswerFromChunkByGroqAsync(ragRequest, context),
            //    AIProvider.OpenRouter => await ExtractAnswerFromChunkByOpenAIAsync(ragRequest, context)
            //};




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





        private async Task<string> ExtractAnswerFromChunkByChatModelAsync(RagUiRequest ragRequest, string chunkContent)
        {
            if (string.IsNullOrEmpty(chunkContent)) return string.Empty;

            try
            {
                // 1. Initialize Kernel and Chat Service
                var kernelBuilder = SemanticKernelHelper.BuildKernel(ragRequest.AIProvider, _providerSettings);
                var kernel = SemanticKernelHelper.Build(kernelBuilder);
                var chatService = kernel.GetRequiredService<IChatCompletionService>();

                // 2. Get existing history or create a new one using your Service
                // Note: Assuming ragRequest contains a SessionId to identify the user
                var chatHistory = _historyService.GetOrCreateHistory(ragRequest.SessionId);

                // 3. To save tokens: We only keep the most recent messages if history is too long
                // You can implement a simple check here if you want to trim old messages from 'chatHistory'

                // 4. Create the current request prompt (User Message)
                // We embed the context and the instructions inside this specific message
                string userPrompt = $@"
<Context>
{chunkContent}
</Context>

Question: {ragRequest.Query}

Instructions:
- Use only the context above to answer.
- Maintain table structure using '|'.
- Provide step-by-step breakdown for any financial calculations.
- Answer in the user's language.";

                // Add the current query to the session history
                chatHistory.AddUserMessage(userPrompt);

                // 5. Execution settings (Temperature 0 for precision)
                var executionSettings = SemanticKernelHelper.GetDefaultPromptSettings(ragRequest.AIProvider);

                // 6. Get AI response
                var response = await chatService.GetChatMessageContentAsync(chatHistory, executionSettings, kernel);
                string answer = response.Content?.Trim() ?? string.Empty;

                // 7. Store the AI's answer in the session history (Crucial for next turns)
                if (!string.IsNullOrEmpty(answer))
                {
                    _historyService.AddAssistantMessage(ragRequest.SessionId, answer);
                }

                return answer;
            }
            catch (Exception ex)
            {
                // Log the error related to the specific AI provider
                throw new ApplicationException($"AI Provider Error ({ragRequest.AIProvider}): {ex.Message}", ex);
            }
        }




    }
}







