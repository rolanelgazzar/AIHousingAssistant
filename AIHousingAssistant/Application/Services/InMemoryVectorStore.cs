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

namespace AIHousingAssistant.Application.Services
{
    public class InMemoryVectorStore : IVectorStore
    {
        private readonly List<VectorChunk> _vectorChunks = new();
        private readonly string _uploadFolder;
        private readonly OllamaApiClient _ollamaEmbeddingClient;
        private readonly ProviderSettings _providerSettings;

        private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

        public InMemoryVectorStore( IOptions<ProviderSettings> providerSettings)
        {
                _uploadFolder = Path.Combine(Directory.GetCurrentDirectory(), providerSettings.Value.ProcessingFolder);

            if (!Directory.Exists(_uploadFolder))
            Directory.CreateDirectory(_uploadFolder);

            _providerSettings = providerSettings.Value;

            _ollamaEmbeddingClient = new OllamaApiClient(_providerSettings.OllamaEmbedding.Endpoint);
            _ollamaEmbeddingClient.SelectedModel = _providerSettings.OllamaEmbedding.Model;
        }

        // -----------------------------------------------------------------




        // -----------------------------------------------------------------
        public async Task  <VectorChunk?> SearchClosest(String queryText)
        {
            float[] queryEmbedding = await TextToVectorAsync(queryText);

            // Build the path to the stored JSON file
            string filePath = Path.Combine(_uploadFolder, _providerSettings.VectorStoreFilename);

            // Return null if the file doesn't exist
            if (!File.Exists(filePath))
                return null;

            // Read the file and deserialize into a list of VectorChunk
            var json = File.ReadAllText(filePath);
            var vectorChunks = JsonSerializer.Deserialize<List<VectorChunk>>(json);

            // Return null if no chunks are available
            if (vectorChunks == null || vectorChunks.Count == 0)
                return null;

            VectorChunk? best = null;
            float bestScore = float.NegativeInfinity;

            // Iterate through all chunks and calculate cosine similarity
            foreach (var chunk in vectorChunks)
            {
                var score = CosineSimilarity(chunk.Embedding, queryEmbedding);
                // Update the best match if current score is higher
                if (score > bestScore)
                {
                    bestScore = score;
                    best = chunk;
                }
            }

            // Return the closest chunk
            return best;
        }
        // Returns all stored vector chunks from memory
        public async Task<List<VectorChunk>> GetAllAsync()
        {
            string filePath = Path.Combine(_uploadFolder, _providerSettings.VectorStoreFilename);

            if (!File.Exists(filePath))
                return new List<VectorChunk>();

            string json = await File.ReadAllTextAsync(filePath);
            var chunks = JsonSerializer.Deserialize<List<VectorChunk>>(json, SerializerOptions);

            return chunks ?? new List<VectorChunk>();
        }


        private float CosineSimilarity(float[] a, float[] b)
        {
            float dot = 0, normA = 0, normB = 0;
            for (int i = 0; i < a.Length; i++)
            {
                dot += a[i] * b[i];
                normA += a[i] * a[i];
                normB += b[i] * b[i];
            }
            return dot / (float)(Math.Sqrt(normA) * Math.Sqrt(normB));
        }

        // -----------------------------------------------------------------
        // Convert text → embedding using Ollama nomic-embed-text
        public async Task<float[]> TextToVectorAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return Array.Empty<float>();

            text = text.Trim().ToLowerInvariant();

            var response = await _ollamaEmbeddingClient.EmbedAsync(text);
            if (response.Embeddings != null && response.Embeddings.Count > 0)
            {
                var embedding = response.Embeddings[0];
                return embedding.Length > 0 ? embedding : Array.Empty<float>();
            }
            return Array.Empty<float>();
        }



        // -----------------------------------------------------------------
        // Convert chunks → vectors + store in memory (parallel for performance)
        public async Task StoreTextChunksAsVectorsAsync(List<TextChunk> chunks)
        {
            var tasks = chunks.Select(async (chunk, i) => new VectorChunk
            {
                Index = chunk.Index,
                Content = chunk.Content,
                Embedding = await TextToVectorAsync(chunk.Content)
            });

            var results = await Task.WhenAll(tasks);
            _vectorChunks.AddRange(results);
          await   SaveVectorStoreAsync(_vectorChunks);
        }

        // -----------------------------------------------------------------
        // Save vector store → JSON
        public async Task SaveVectorStoreAsync( List<VectorChunk> vectorChunks)
        {
            string filePath = Path.Combine(_uploadFolder, _providerSettings.VectorStoreFilename);
            string json = JsonSerializer.Serialize(vectorChunks, SerializerOptions);
            await File.WriteAllTextAsync(filePath, json);
        }
    }
}