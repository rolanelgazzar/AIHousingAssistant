//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Text.Json;
//using System.Threading.Tasks;
//using AIHousingAssistant.Models;
//using AIHousingAssistant.Models.Settings;
//using Microsoft.Extensions.Options;
//using OllamaSharp;
//using System.Linq;
//using AIHousingAssistant.Application.Services.Interfaces;
//using AIHousingAssistant.Helper;
//using AIHousingAssistant.Application.Services.Embedding;
//using AIHousingAssistant.Application.Enum;
//using AIHousingAssistant.Application.Services.VectorDb;

//namespace AIHousingAssistant.Application.Services.VectorStores
//{
//    public class InMemoryVectorStore : IVectorStore
//    {
//        private readonly List<VectorChunk> _vectorChunks = new();
//        private readonly string _uploadFolder;

//        private readonly IVectorDB _vectorDB;
//        private readonly ProviderSettings _providerSettings;

//        // NEW: centralized embedding service
//        private readonly IEmbeddingService _embeddingService;

//        private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

//        public InMemoryVectorStore(IOptions<ProviderSettings> providerSettings,
//            IEmbeddingService embeddingService,
//            IVectorDB vectorDB
//            )
//        {
//            _embeddingService = embeddingService;

//            _uploadFolder = Path.Combine(Directory.GetCurrentDirectory(), providerSettings.Value.ProcessingFolder);

//            if (!Directory.Exists(_uploadFolder))
//                Directory.CreateDirectory(_uploadFolder);

//            _providerSettings = providerSettings.Value;

//            _vectorDB = vectorDB;

//        }

//        // -----------------------------------------------------------------
//        // Find the closest stored chunk to the given query text (cosine similarity)
//        public async Task<VectorChunk?> VectorSearchAsync(string queryText)
//        {
//            // Convert query text to embedding
//            float[] queryEmbedding = await _embeddingService.EmbedAsync(queryText);

//            // Build the path to the stored JSON file (kept for reference)
//            string filePath = Path.Combine(_uploadFolder, _providerSettings.VectorStoreFilename);

//            // Return null if the file doesn't exist (kept for reference)
//            if (!File.Exists(filePath))
//                return null;

//            // Read the stored JSON file
//            var vectorChunks = await FileHelper.ReadJsonAsync<List<VectorChunk>>(
//                _uploadFolder,
//                _providerSettings.VectorStoreFilename
//            );

//            // Return null if no chunks are available
//            if (vectorChunks == null || vectorChunks.Count == 0)
//                return null;

//            VectorChunk? bestChunk = null;
//            float bestScore = float.NegativeInfinity;

//            // Iterate through all chunks and calculate cosine similarity
//            foreach (var chunk in vectorChunks)
//            {
//                var score = SearchHelper.CosineSimilarity(chunk.Embedding, queryEmbedding);

//                // Update the best match if current score is higher
//                if (score > bestScore)
//                {
//                    bestScore = score;
//                    bestChunk = chunk;
//                }
//            }

//            // Return the closest chunk
//            return bestChunk;
//        }

//        // -----------------------------------------------------------------
//        // Returns all stored vector chunks from disk (JSON)
//        public async Task<List<VectorChunk>> GetAllAsync()
//        {
//            string filePath = Path.Combine(_uploadFolder, _providerSettings.VectorStoreFilename);

//            if (!File.Exists(filePath))
//                return new List<VectorChunk>();

//            var chunks = await FileHelper.ReadJsonAsync<List<VectorChunk>>(
//                _uploadFolder,
//                _providerSettings.VectorStoreFilename
//            );

//            return chunks ?? new List<VectorChunk>();
//        }

//        // -----------------------------------------------------------------
//        // Safe cosine similarity (avoids divide-by-zero & dimension mismatch)
        

//        // -----------------------------------------------------------------
//        // Convert chunks → vectors + store in memory (parallel for performance)
//        public async Task StoreTextChunksAsVectorsAsync(List<TextChunk> chunks, EmbeddingModel embeddingModel)
//        {
//            var tasks = chunks.Select(async (chunk, i) => new VectorChunk
//            {
//                Index = chunk.Index,
//                Content = chunk.Content,

//                // IMPORTANT: keep source traceability (requires VectorChunk.Source property)
//                Source = chunk.Source,

//                Embedding = await _embeddingService.EmbedAsync(chunk.Content  , embeddingModel)
//            });

//            var results = await Task.WhenAll(tasks);
//            _vectorChunks.AddRange(results);


//            string embeddingModelName = System.Enum.GetName(typeof(EmbeddingModel), embeddingModel);
//            var fileName = $"{_providerSettings.VectorStoreFilename}-{embeddingModelName}.json";

//            await FileHelper.WriteJsonAsync(_uploadFolder, fileName, _vectorChunks); 
//            await FileHelper.WriteJsonAsync(_uploadFolder, _providerSettings.VectorStoreFilename, _vectorChunks);
//        }




//    public     async Task<List<VectorChunk>> HybridSearchAsync(
//        string queryText,
//        int top)
//        {
//            if (string.IsNullOrWhiteSpace(queryText))
//                return new List<VectorChunk>();

//            var queryEmbedding = await _embeddingService.EmbedAsync(queryText);
//            if (queryEmbedding == null || queryEmbedding.Length == 0)
//                return new List<VectorChunk>();

//            var queryKeywords = SearchHelper.ExtractKeywords(queryText);

//            string filePath = Path.Combine(_uploadFolder, _providerSettings.VectorStoreFilename);
//            if (!File.Exists(filePath))
//                return new List<VectorChunk>();

//            var chunks = await FileHelper.ReadJsonAsync<List<VectorChunk>>(
//                _uploadFolder,
//                _providerSettings.VectorStoreFilename
//            );

//            if (chunks == null || chunks.Count == 0)
//                return new List<VectorChunk>();

//            return chunks
//                .Select(chunk =>
//                {
//                    var semanticScore = SearchHelper.CosineSimilarity(chunk.Embedding, queryEmbedding);

//                    var keywordScore = 0f;
//                    if (!string.IsNullOrWhiteSpace(chunk.Content))
//                    {
//                        var contentKeywords = SearchHelper.ExtractKeywords(chunk.Content);
//                        keywordScore = contentKeywords
//                            .Intersect(queryKeywords)
//                            .Count();
//                    }

//                    // simple hybrid fusion
//                    var finalScore = semanticScore + (0.1f * keywordScore);

//                    return new
//                    {
//                        Chunk = chunk,
//                        Score = finalScore
//                    };
//                })
//                .OrderByDescending(x => x.Score)
//                .Take(top)
//                .Select(x => x.Chunk)
//                .ToList();
//        }

//      public  async Task<List<VectorChunk>> SemanticSearchAsync(
//     string queryText,
//     int top)
//        {
//            if (string.IsNullOrWhiteSpace(queryText))
//                return new List<VectorChunk>();

//            var queryEmbedding = await _embeddingService.EmbedAsync(queryText);
//            if (queryEmbedding == null || queryEmbedding.Length == 0)
//                return new List<VectorChunk>();

//            string filePath = Path.Combine(_uploadFolder, _providerSettings.VectorStoreFilename);
//            if (!File.Exists(filePath))
//                return new List<VectorChunk>();

//            var chunks = await FileHelper.ReadJsonAsync<List<VectorChunk>>(
//                _uploadFolder,
//                _providerSettings.VectorStoreFilename
//            );

//            if (chunks == null || chunks.Count == 0)
//                return new List<VectorChunk>();

//            return chunks
//                .Select(c => new
//                {
//                    Chunk = c,
//                    Score = SearchHelper.CosineSimilarity(c.Embedding, queryEmbedding)
//                })
//                .OrderByDescending(x => x.Score)
//                .Take(top)ش
//                .Select(x => x.Chunk)
//                .ToList();
//        }

 

   
//    }
//}
