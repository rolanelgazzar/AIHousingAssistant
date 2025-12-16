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

namespace AIHousingAssistant.Application.Services
{
    public class RagService : IRagService
    {
        private readonly ProviderSettings _providerSettings;
        private readonly IChunkService _chunkService;
        private readonly OllamaApiClient _ollamaClient;
        private readonly IVectorStore _vectorStore;

        // NEW: Use resolver instead of injecting 3 stores

        public RagService(
            IOptions<ProviderSettings> providerSettings,
            IChunkService chunkService,
            IVectorStore vectorStore)
        {
            if (providerSettings == null)
                throw new ArgumentNullException(nameof(providerSettings));

            _providerSettings = providerSettings.Value;
            _chunkService = chunkService ?? throw new ArgumentNullException(nameof(chunkService));
            _vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
            // Initialize Ollama client for answer generation (llama3)
            _ollamaClient = new OllamaApiClient(new Uri(_providerSettings.Ollama.Endpoint));
            _ollamaClient.SelectedModel = _providerSettings.Ollama.Model;
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
           var textExtracted= await FileHelper.ExtractDocumentAsync(filePath, source);
           

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
            var answer = await ExtractAnswerFromChunkAsync(ragRequest.Query, context);

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


        // --------------------------------------------


        // --------------------------------------------
        private async Task<string> ExtractAnswerFromChunkAsync(string query, string chunkContent)
        {
           // return chunkContent;

            //return chunkContent;
            if (string.IsNullOrWhiteSpace(chunkContent))
                return string.Empty;

            if (string.IsNullOrWhiteSpace(query))
                return chunkContent.Trim();

            var prompt = $@"
You are an AI assistant for a housing and maintenance system.

You will receive:
- CONTEXT: a piece of a knowledge base.
- QUESTION: a user question.

Your task:
- Answer the QUESTION using ONLY the information in the CONTEXT.
- If the answer is present, respond with a short, direct answer (one or two sentences).
- Do NOT mention the word CONTEXT.
- Do NOT include unrelated information.
- If the answer is not in the CONTEXT, say: ""I don't know based on the provided information.""

CONTEXT:
{chunkContent}

QUESTION:
{query}

ANSWER:
";

            var sb = new StringBuilder();

            await foreach (var response in _ollamaClient.GenerateAsync(prompt))
            {
                if (!string.IsNullOrEmpty(response.Response))
                    sb.Append(response.Response);
            }

            return sb.ToString().Trim();
        }



 
    }
}
