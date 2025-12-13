using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using AIHousingAssistant.Models;
using AIHousingAssistant.Models.Settings;
using Microsoft.Extensions.Options;
using OllamaSharp;
using System.Linq;
using AIHousingAssistant.Application.Services.Interfaces;
using AIHousingAssistant.Helper;
using AIHousingAssistant.Application.Services.Embedding;

namespace AIHousingAssistant.Application.Services.VectorStores
{
    public class InMemoryVectorStore : IInMemoryVectorStore
    {
        private readonly List<VectorChunk> _vectorChunks = new();
        private readonly string _uploadFolder;


        private readonly ProviderSettings _providerSettings;

        // NEW: centralized embedding service
        private readonly IEmbeddingService _embeddingService;

        private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

        public InMemoryVectorStore(IOptions<ProviderSettings> providerSettings, IEmbeddingService embeddingService)
        {
            _embeddingService = embeddingService;

            _uploadFolder = Path.Combine(Directory.GetCurrentDirectory(), providerSettings.Value.ProcessingFolder);

            if (!Directory.Exists(_uploadFolder))
                Directory.CreateDirectory(_uploadFolder);

            _providerSettings = providerSettings.Value;

        }

        // -----------------------------------------------------------------
        // Find the closest stored chunk to the given query text (cosine similarity)
        public async Task<VectorChunk?> SearchClosest(string queryText)
        {
            // Convert query text to embedding
            float[] queryEmbedding = await _embeddingService.EmbedAsync(queryText);

            // Build the path to the stored JSON file (kept for reference)
            string filePath = Path.Combine(_uploadFolder, _providerSettings.VectorStoreFilename);

            // Return null if the file doesn't exist (kept for reference)
            if (!File.Exists(filePath))
                return null;

            // Read the stored JSON file
            var vectorChunks = await FileHelper.ReadJsonAsync<List<VectorChunk>>(
                _uploadFolder,
                _providerSettings.VectorStoreFilename
            );

            // Return null if no chunks are available
            if (vectorChunks == null || vectorChunks.Count == 0)
                return null;

            VectorChunk? bestChunk = null;
            float bestScore = float.NegativeInfinity;

            // Iterate through all chunks and calculate cosine similarity
            foreach (var chunk in vectorChunks)
            {
                var score = CosineSimilarity(chunk.Embedding, queryEmbedding);

                // Update the best match if current score is higher
                if (score > bestScore)
                {
                    bestScore = score;
                    bestChunk = chunk;
                }
            }

            // Return the closest chunk
            return bestChunk;
        }

        // -----------------------------------------------------------------
        // Returns all stored vector chunks from disk (JSON)
        public async Task<List<VectorChunk>> GetAllAsync()
        {
            string filePath = Path.Combine(_uploadFolder, _providerSettings.VectorStoreFilename);

            if (!File.Exists(filePath))
                return new List<VectorChunk>();

            var chunks = await FileHelper.ReadJsonAsync<List<VectorChunk>>(
                _uploadFolder,
                _providerSettings.VectorStoreFilename
            );

            return chunks ?? new List<VectorChunk>();
        }

        // -----------------------------------------------------------------
        // Safe cosine similarity (avoids divide-by-zero & dimension mismatch)
        private float CosineSimilarity(float[] a, float[] b)
        {
            if (a == null || b == null || a.Length == 0 || b.Length == 0)
                return 0f;

            if (a.Length != b.Length)
                return 0f;

            float dot = 0, normA = 0, normB = 0;

            for (int i = 0; i < a.Length; i++)
            {
                dot += a[i] * b[i];
                normA += a[i] * a[i];
                normB += b[i] * b[i];
            }

            const float eps = 1e-6f;
            if (normA < eps || normB < eps)
                return 0f;

            return dot / (float)(Math.Sqrt(normA) * Math.Sqrt(normB));
        }


        // -----------------------------------------------------------------
        // Convert chunks → vectors + store in memory (parallel for performance)
        public async Task StoreTextChunksAsVectorsAsync(List<TextChunk> chunks)
        {
            var tasks = chunks.Select(async (chunk, i) => new VectorChunk
            {
                Index = chunk.Index,
                Content = chunk.Content,

                // IMPORTANT: keep source traceability (requires VectorChunk.Source property)
                Source = chunk.Source,

                Embedding = await _embeddingService.EmbedAsync(chunk.Content)
            });

            var results = await Task.WhenAll(tasks);
            _vectorChunks.AddRange(results);

            await FileHelper.WriteJsonAsync(_uploadFolder, _providerSettings.VectorStoreFilename, _vectorChunks);
        }
    }
}
