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

        // NEW: Use resolver instead of injecting 3 stores
        private readonly IVectorStoreResolver _vectorStoreResolver;

        public RagService(
            IOptions<ProviderSettings> providerSettings,
            IChunkService chunkService,
            IVectorStoreResolver vectorStoreResolver)
        {
            if (providerSettings == null)
                throw new ArgumentNullException(nameof(providerSettings));

            _providerSettings = providerSettings.Value;

            _chunkService = chunkService ?? throw new ArgumentNullException(nameof(chunkService));
            _vectorStoreResolver = vectorStoreResolver ?? throw new ArgumentNullException(nameof(vectorStoreResolver));

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
            var chunks = await _chunkService.CreateChunksAsync(textExtracted, ragUiRequest.ChunkingMode, source);

            if (chunks == null || chunks.Count == 0)
                throw new InvalidOperationException("No text chunks were generated from the document.");

            // 4) Store vectors in the selected vector store
            var store = _vectorStoreResolver.Resolve(ragUiRequest.VectorStoreProvider);
            await store.StoreTextChunksAsVectorsAsync(chunks, ragUiRequest.EmbeddingModel );
        }


        // --------------------------------------------
        // New unified method
        public async Task<RagAnswerResponse> AskRagAsync(RagUiRequest ragRequest)
        {
            if (string.IsNullOrWhiteSpace(ragRequest.Query))
                return new RagAnswerResponse { Answer = "Query is empty." };

            var store = _vectorStoreResolver.Resolve(ragRequest.VectorStoreProvider);

            List<VectorChunk> chunks = ragRequest.SearchMode switch
            {
                SearchMode.Hybrid => await store.HybridSearchAsync(ragRequest.Query, top: 5),
                SearchMode.Semantic => await store.SemanticSearchAsync(ragRequest.Query, top: 5),
                _ => (await store.VectorSearchAsync(ragRequest.Query)) is { } c
                        ? new List<VectorChunk> { c }
                        : new List<VectorChunk>()
            };

            if (chunks == null || chunks.Count == 0)
                return new RagAnswerResponse { Answer = "No related answer found." };

            var usedChunks = chunks
                .Where(c => !string.IsNullOrWhiteSpace(c.Content))
                .ToList();

            if (usedChunks.Count == 0)
                return new RagAnswerResponse { Answer = "No related answer found." };

            var context = string.Join("\n\n---\n\n", usedChunks.Select(c => c.Content));

            var answer = await ExtractAnswerFromChunkAsync(ragRequest.Query, context);

            if (string.IsNullOrWhiteSpace(answer))
                answer = "No related answer found.";

            return new RagAnswerResponse
            {
                Answer = answer,
                ChunkIndexes = usedChunks.Select(c => c.Index).Distinct().ToList(),
                Sources = usedChunks.Select(c => c.Source).Distinct().ToList() ,
                Similarity = usedChunks.Select(c => c.Similarity).Distinct().ToList()
            };
        }



        // --------------------------------------------


        // --------------------------------------------
        private async Task<string> ExtractAnswerFromChunkAsync(string query, string chunkContent)
        {

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
