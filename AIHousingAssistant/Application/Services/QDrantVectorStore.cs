using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AIHousingAssistant.Application.Services.Interfaces;
using AIHousingAssistant.Models;
using AIHousingAssistant.Models.Settings;
using Microsoft.Extensions.Options;
using OllamaSharp;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace AIHousingAssistant.Application.Services
{
    public class QDrantVectorStore : IQDrantVectorStore
    {
        private readonly QdrantClient _clientQdrant;
        private readonly ProviderSettings _providerSettings;
        private readonly OllamaApiClient _ollamaEmbeddingClient;
        private readonly string _uploadFolder;

        private const string CollectionName = "housing_vectors";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public QDrantVectorStore(IOptions<ProviderSettings> providerSettings)
        {
            if (providerSettings is null)
                throw new ArgumentNullException(nameof(providerSettings));

            _providerSettings = providerSettings.Value;

            _clientQdrant = new QdrantClient(_providerSettings.QDrant.Endpoint);

            // Folder where chunks JSON is stored
            _uploadFolder = Path.Combine(
                Directory.GetCurrentDirectory(),
                _providerSettings.ProcessingFolder
            );

            if (!Directory.Exists(_uploadFolder))
                Directory.CreateDirectory(_uploadFolder);

            // Ollama client used for embeddings
            _ollamaEmbeddingClient = new OllamaApiClient(_providerSettings.OllamaEmbedding.Endpoint);
            _ollamaEmbeddingClient.SelectedModel = _providerSettings.OllamaEmbedding.Model;
        }

        // --------------------------------------------------------------------
        // Ensure the collection exists in Qdrant (create if not)
        // --------------------------------------------------------------------
        private async Task<bool> CollectionExistsAsync(string collectionName)
        {
            var host = _providerSettings.QDrant.Endpoint?.Trim(); // e.g. "localhost"
            if (string.IsNullOrWhiteSpace(host))
                throw new InvalidOperationException("Qdrant endpoint is not configured.");

            var baseUri = new Uri($"http://{host}:6333");

            using var http = new HttpClient { BaseAddress = baseUri };

            var response = await http.GetAsync($"/collections/{collectionName}/exists");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(json);
            // JSON: { "result": { "exists": true }, "status": "ok", ... }
            return doc.RootElement
                .GetProperty("result")
                .GetProperty("exists")
                .GetBoolean();
        }
        
private async Task EnsureCollectionAsync(int vectorSize)
    {
        // 1) Check if the collection already exists
        var exists = await CollectionExistsAsync(CollectionName);
        if (exists)
        {
            // Optional: you can log if you want
            // Console.WriteLine($"[QDRANT] Collection '{CollectionName}' already exists.");
            return;
        }

        // 2) Create the collection if it does NOT exist
        await _clientQdrant.CreateCollectionAsync(
            CollectionName,
            new VectorParams
            {
                Size = (ulong)vectorSize,
                Distance = Distance.Cosine
            });
    }

        // --------------------------------------------------------------------
        // Store chunks as vectors in Qdrant
        // Qdrant: only stores Id + Vector (no need to rely on Payload at all)
        // --------------------------------------------------------------------
        public async Task StoreTextChunksAsVectorsAsync(List<TextChunk> chunks)
        {
            if (chunks == null || chunks.Count == 0)
                return;

            // 1) Generate embedding for each chunk in parallel
            var embedded = await Task.WhenAll(
                chunks.Select(async c => new
                {
                    Chunk = c,
                    Embedding = await TextToVectorAsync(c.Content)
                })
            );

            // 2) Keep only chunks that have a valid embedding
            var valid = embedded
                .Where(x => x.Embedding is { Length: > 0 })
                .ToList();

            if (valid.Count == 0)
                return;

            // 3) Ensure the collection exists with the correct vector size
            await EnsureCollectionAsync(valid[0].Embedding.Length);

            // 4) Map to Qdrant points (vector + payload)
            var points = valid.Select(x => new PointStruct
            {
                // Use chunk.Index as Qdrant point id
                Id = (ulong)x.Chunk.Index,

                // ✅ This is where the embedding is stored
                Vectors = x.Embedding,

                // Optional metadata (helpful for debugging / direct reading from Qdrant)
                Payload =
        {
            ["index"]   = x.Chunk.Index,
            ["content"] = x.Chunk.Content,
            ["source"]  = x.Chunk.Source ?? string.Empty
        }
            }).ToList();

            // 5) Upsert all points to Qdrant
            await _clientQdrant.UpsertAsync(CollectionName, points);
        }


        // --------------------------------------------------------------------
        // Search for the closest chunk to the query text
        // 1) Embed the query
        // 2) Ask Qdrant for the top-1 closest point
        // 3) Use the returned Id as Chunk.Index
        // 4) Load the real text from the local chunks JSON file
        // --------------------------------------------------------------------
        public async Task<VectorChunk?> SearchClosest(string queryText)
        {
            if (string.IsNullOrWhiteSpace(queryText))
                return null;

            try
            {
                // 1) Convert the query text into an embedding
                var embedding = await TextToVectorAsync(queryText);
                if (embedding == null || embedding.Length == 0)
                    return null;

                // 2) Search in Qdrant for the top-1 closest point
                var scoredPoints = await _clientQdrant.SearchAsync(
                    CollectionName,
                    embedding,
                    limit: 1
                );

                var best = scoredPoints.FirstOrDefault();
                if (best == null)
                    return null;

                // 3) Extract the numeric id we stored as (ulong)chunk.Index
                //    We always store numeric ids, so we can safely read Num.
                var index = (int)best.Id.Num;

                // 4) Load the original chunk content from local JSON (chunks file)
                var chunk = await LoadChunkByIndexAsync(index);
                if (chunk == null || string.IsNullOrWhiteSpace(chunk.Content))
                    return null;

                Console.WriteLine($"[QDRANT] Search best index={index}, score={best.Score}");

                // 5) Return a VectorChunk for the RAG layer
                return new VectorChunk
                {
                    Index = index,
                    Content = chunk.Content,
                    Similarity = best.Score,
                    // We do not need to return the vector itself here
                    Embedding = Array.Empty<float>()
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[QDRANT] SearchClosest error: {ex.Message}");
                Console.WriteLine(ex);
                throw;
            }
        }

        // --------------------------------------------------------------------
        // GetAllAsync – not really supported by Qdrant in a simple way.
        // We keep it as "not implemented" and just return an empty list for now.
        // If you really need it later, we can implement scrolling through all points.
        // --------------------------------------------------------------------
        public Task<List<VectorChunk>> GetAllAsync()
        {
            return Task.FromResult(new List<VectorChunk>());
        }

        // --------------------------------------------------------------------
        // Convert text → embedding using Ollama
        // --------------------------------------------------------------------
        public async Task<float[]> TextToVectorAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return Array.Empty<float>();

            text = text.Trim().ToLowerInvariant();

            var response = await _ollamaEmbeddingClient.EmbedAsync(text);

            if (response.Embeddings != null && response.Embeddings.Count > 0)
            {
                var embedding = response.Embeddings[0];
                return embedding != null && embedding.Length > 0
                    ? embedding
                    : Array.Empty<float>();
            }

            return Array.Empty<float>();
        }

        // --------------------------------------------------------------------
        // Helper: load a TextChunk from the local chunks JSON by its Index
        // --------------------------------------------------------------------
        private async Task<TextChunk?> LoadChunkByIndexAsync(int index)
        {
            // Prefer ChunksFileName if it is set, otherwise fall back to VectorStoreFilename
            var fileName = !string.IsNullOrWhiteSpace(_providerSettings.ChunksFileName)
                ? _providerSettings.ChunksFileName
                : _providerSettings.VectorStoreFilename;

            var filePath = Path.Combine(_uploadFolder, fileName);

            if (!File.Exists(filePath))
                return null;

            var json = await File.ReadAllTextAsync(filePath);
            var chunks = JsonSerializer.Deserialize<List<TextChunk>>(json, JsonOptions);

            return chunks?.FirstOrDefault(c => c.Index == index);
        }
    }
}
