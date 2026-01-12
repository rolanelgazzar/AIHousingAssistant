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
using AIHousingAssistant.Application.SemanticKernel;
using AIHousingAssistant.Application.Services.ChatTools.Interfaces;

using AIHousingAssistant.Application.Services.RagPipeline.Abstractions;
namespace AIHousingAssistant.Application.Services.ChatTools
{
    public class RagMDSharpPipelineService 
    {
        private readonly Settings _settings;
        private readonly IChunkService _chunkService;
        private readonly OllamaApiClient _ollamaClient;
        private readonly IVectorStore _vectorStore;
        private readonly Kernel _kernel;
        private readonly IChatHistoryService _historyService;
        private readonly  IRagMDSharpPipelineProcessor _ragPipelineSharpProcessor;
        // NEW: Use resolver instead of injecting 3 stores

        public RagMDSharpPipelineService(
            IOptions<Settings> providerSettings,
            IChunkService chunkService,
            IVectorStore vectorStore,
            IChatHistoryService historyService,
            Kernel kernel,
            IRagMDSharpPipelineProcessor ragPipelineSharpProcessor
            )
        {
            if (providerSettings == null)
                throw new ArgumentNullException(nameof(providerSettings));

            _settings = providerSettings.Value;
            _chunkService = chunkService ?? throw new ArgumentNullException(nameof(chunkService));
            _vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
            // Initialize Ollama client for answer generation (llama3)
            _ollamaClient = new OllamaApiClient(new Uri(_settings.Ollama.Endpoint));
            _ollamaClient.SelectedModel = _settings.Ollama.Model;
            _historyService = historyService ?? throw new ArgumentNullException(nameof(historyService));
            _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
            _ragPipelineSharpProcessor = ragPipelineSharpProcessor ?? throw new ArgumentNullException(nameof(ragPipelineSharpProcessor));
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
                    //var filePath = await FileHelper.SaveFileAsync(file, _settings.ProcessingFolder);
                    //var source = FileHelper.GetSafeFileNameFromPath(filePath);

                    //// 2) Extract text from the saved document
                    //// var textExtracted = await FileHelper.ExtractDocumentAsync(filePath, source);
                    // This will: Save -> Convert (MarkItDown) -> MetaData (Language) -> Normalize
                    var docProcessed = await _ragPipelineSharpProcessor.ExecutePipelineAsync(file, ragUiRequest);

                    
                }
                catch (Exception ex)
                {
                    // You can log the error for a specific file here and continue with others
                    // _logger.LogError($"Error processing file {file.FileName}: {ex.Message}");
                    throw; // Or rethrow if you want to stop the entire process
                }
            }
        }
        #region old code ProcessDocumentByRagAsync
        //public async Task ProcessDocumentByRagAsync(List<IFormFile> files, RagUiRequest ragUiRequest)
        //{
        //    // Check if the list is null or empty
        //    if (files == null || files.Count == 0)
        //        throw new ArgumentException("No files provided for processing.");

        //    // Loop through each file in the list
        //    foreach (var file in files)
        //    {
        //        // Skip empty files
        //        if (file == null || file.Length == 0)
        //            continue;

        //        try
        //        {
        //            //// 1) Save the file locally to the processing folder
        //            //var filePath = await FileHelper.SaveFileAsync(file, _settings.ProcessingFolder);
        //            //var source = FileHelper.GetSafeFileNameFromPath(filePath);

        //            //// 2) Extract text from the saved document
        //            //// var textExtracted = await FileHelper.ExtractDocumentAsync(filePath, source);
        //            // This will: Save -> Convert (MarkItDown) -> MetaData (Language) -> Normalize
        //            var docProcessed = await _docProcessor.ProcessAndSaveAsync(file);

        //            // Get safe name for source tracking
        //            var source = Path.GetFileName(docProcessed.FilePath);

        //            // Read the clean markdown content to be chunked
        //            var cleanText = docProcessed.Content; //await File.ReadAllTextAsync(markdownFilePath);



        //            // 3) Split text into chunks based on the selected RagUiRequest configuration
        //            var chunks = await _chunkService.CreateChunksAsync(cleanText, ragUiRequest, source);

        //            if (chunks == null || chunks.Count == 0)
        //            {
        //                // Log or handle files that produce no content
        //                continue;
        //            }

        //            // 4) Store generated vectors in the selected Vector Database
        //            await _vectorStore.StoreTextChunksAsVectorsAsync(chunks, ragUiRequest);
        //        }
        //        catch (Exception ex)
        //        {
        //            // You can log the error for a specific file here and continue with others
        //            // _logger.LogError($"Error processing file {file.FileName}: {ex.Message}");
        //            throw; // Or rethrow if you want to stop the entire process
        //        }
        //    }
        //}
        #endregion
        // ----------------------------------s----------
        // New unified method
        public async Task<RagAnswerResponse> AskRagAsync(RagUiRequest ragRequest)
        {
            // Validate the incoming query
            if (string.IsNullOrWhiteSpace(ragRequest.Query))
                return new RagAnswerResponse { Answer = "Query is empty." };

            // 1. Retrieval (Vector Retrieval)
            List<VectorChunk>? chunks = ragRequest.SearchMode switch
            {
                SearchMode.Hybrid => await _vectorStore.HybridSearchAsync(ragRequest.Query, ragRequest),
                SearchMode.Semantic => await _vectorStore.SemanticSearchAsync(ragRequest.Query, ragRequest),
                SearchMode.Vector => await _vectorStore.VectorSearchAsync(ragRequest.Query, ragRequest) is { } singleChunk
                                     ? new List<VectorChunk> { singleChunk }
                                     : new List<VectorChunk>(),
                _ => await _vectorStore.VectorSearchAsync(ragRequest.Query, ragRequest) is { } defaultChunk
                     ? new List<VectorChunk> { defaultChunk }
                     : new List<VectorChunk>()
            };

            if (chunks == null || chunks.Count == 0)
                return new RagAnswerResponse { Answer = "No related answer found in Vector DB." };

            var usedChunks = chunks.Where(c => !string.IsNullOrWhiteSpace(c.Content)).ToList();

            if (usedChunks.Count == 0)
                return new RagAnswerResponse { Answer = "No related content found." };

            // --- DEBUG / SHORT CIRCUIT LOGIC ---
            // If the mode is Pure Vector, we return the chunks directly without LLM processing
            if (ragRequest.SearchMode == SearchMode.Vector || ragRequest.SearchMode == SearchMode.Semantic)
            {
                return new RagAnswerResponse
                {
                    Answer =  string.Join("\n\n---\n\n", usedChunks.Select(c => c.Content)),
                    ChunkIndexes = usedChunks.Select(c => c.Index).Distinct().ToList(),
                    Sources = usedChunks.Select(c => c.Source).Distinct().ToList(),
                    Similarity = usedChunks.Select(c => c.Similarity).ToList()
                };
            }

            // 2. Generation (Answer Synthesis) - Only for Semantic or Hybrid modes
            var context = string.Join("\n\n---\n\n", usedChunks.Select(c => c.Content));

            // Call the LLM only if we are not in pure Vector Debug mode
            string? answer = await ExtractAnswerFromChunkByChatModelAsync(ragRequest, context);

            if (string.IsNullOrWhiteSpace(answer))
                answer = "No related answer found by the Chat Model.";

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
                var kernelBuilder = SemanticKernelHelper.BuildKernel(ragRequest.AIProvider, _settings);
                var kernel = SemanticKernelHelper.Build(kernelBuilder);
                var chatService = kernel.GetRequiredService<IChatCompletionService>();

                var chatHistory = _historyService.GetOrCreateHistory(ragRequest.SessionId);

                // Clear history if disabled to keep the prompt clean and focused
                if (_settings.EnableChatHistory == false)
                {
                    chatHistory.Clear();
                }

                // Using the same structure as GetEnrichedQuery for consistency
                string userPrompt = $@"
[System: Banking assistant. Use context only. Rules:
1. Respond in the EXACT same language as the user query.
2. If data is tabular, you MUST use a Markdown table. 
   - Ensure each row is on a NEW LINE.
   - Start with the header row, then the separator row '|---|---|', then data rows.
3. No external info. If missing, say: 'Information not available'.]

### Context:
{chunkContent}

### User: {ragRequest.Query}";

                chatHistory.AddUserMessage(userPrompt);

                var executionSettings = SemanticKernelHelper.GetDefaultPromptSettings(ragRequest.AIProvider);

                var response = await chatService.GetChatMessageContentAsync(chatHistory, executionSettings, kernel);
                string answer = response.Content?.Trim() ?? string.Empty;

                if (_settings.EnableChatHistory && !string.IsNullOrEmpty(answer))
                {
                    _historyService.AddAssistantMessage(ragRequest.SessionId, answer);
                }

                return answer;
            }
            catch (Exception ex)
            {
                throw new ApplicationException($"AI Provider Error ({ragRequest.AIProvider}): {ex.Message}", ex);
            }
        }

    }
}







