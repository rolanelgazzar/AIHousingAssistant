//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Threading.Tasks;
//using AIHousingAssistant.Application.Services.Interfaces;
//using AIHousingAssistant.Helper;
//using AIHousingAssistant.Models;
//using AIHousingAssistant.Models.Settings;
//using Microsoft.Extensions.Options;

//// NOTE: This class replaces the low-level logic (File I/O, Cosine Similarity) 
//// previously found in InMemoryVectorStore.

//namespace AIHousingAssistant.Application.Services.VectorDb
//{
//    public class c : IVectorDB
//    {
//        private readonly Dictionary<string, List<VectorChunk>> _inMemoryCollections = new();
//        private readonly string _storagePath;

//        public QdrantVectorDb_InMemory(IOptions<ProviderSettings> providerSettings)
//        {
//            var settings = providerSettings.Value;
//            // Use the same file system logic as before
//            _storagePath = Path.Combine(Directory.GetCurrentDirectory(), settings.ProcessingFolder);
//            if (!Directory.Exists(_storagePath))
//                Directory.CreateDirectory(_storagePath);
//        }

//        // --- IVectorDB Implementation ---

//        // 1. Storage Operations
//        public Task EnsureCollectionAsync(string collectionName, int vectorSize)
//        {
//            // In-Memory: No actual collection creation needed, but we check if it exists in memory.
//            if (!_inMemoryCollections.ContainsKey(collectionName))
//            {
//                _inMemoryCollections[collectionName] = new List<VectorChunk>();
//            }
//            // For file-based persistence: The file name can be assumed based on the collectionName.
//            return Task.CompletedTask;
//        }

//        public async Task UpsertAsync(string collectionName, List<VectorChunk> vectors)
//        {
//            // Update in-memory storage (Append/Overwrite based on Index/ID logic, but for simplicity, we just append)
//            if (_inMemoryCollections.TryGetValue(collectionName, out var currentList))
//            {
//                currentList.AddRange(vectors);
//            }
//            else
//            {
//                _inMemoryCollections[collectionName] = vectors;
//            }

//            // Write to disk for persistence (Using collectionName as filename base)
//            string fileName = $"{collectionName}.json";
//            await FileHelper.WriteJsonAsync(_storagePath, fileName, _inMemoryCollections[collectionName]);
//        }

//        // 2. Search Operations
//        public async Task<List<VectorChunk>> SearchAsync(
//            string collectionName,
//            float[] queryVector,
//            int top,
//            object? filter,
//            bool withPayload)
//        {
//            // 1. Load data from memory (or disk if not loaded)
//            if (!_inMemoryCollections.ContainsKey(collectionName))
//            {
//                string fileName = $"{collectionName}.json";
//                var chunks = await FileHelper.ReadJsonAsync<List<VectorChunk>>(_storagePath, fileName);
//                _inMemoryCollections[collectionName] = chunks ?? new List<VectorChunk>();
//            }

//            var storedChunks = _inMemoryCollections[collectionName];

//            if (storedChunks == null || storedChunks.Count == 0)
//                return new List<VectorChunk>();

//            // 2. Apply filtering (Hybrid Search Logic)
//            var filteredChunks = ApplyFilter(storedChunks, filter);

//            // 3. Calculate similarity and rank (Semantic Search Logic)
//            return filteredChunks
//                .Select(chunk =>
//                {
//                    // Calculate similarity using the helper (the core of in-memory search)
//                    var semanticScore = SearchHelper.CosineSimilarity(chunk.Embedding, queryVector);

//                    // Note: We update the chunk's score field here for the final list
//                    chunk.Similarity = semanticScore;

//                    return chunk;
//                })
//                .Where(c => c.Embedding != null && c.Embedding.Length > 0) // Basic validation
//                .OrderByDescending(c => c.Similarity)
//                .Take(top)
//                .ToList();
//        }

//        // 3. Helper for Filter Application (Hybrid Search)
//        private List<VectorChunk> ApplyFilter(List<VectorChunk> chunks, object? filter)
//        {
//            // In a real DB, Qdrant handles this. Here, we must parse the filter object.

//            // This example only handles the simple keyword filter structure from VectorStore
//            if (filter is not null)
//            {
//                // We'll assume the filter is the simple anonymous object created in VectorStore.HybridSearchAsync
//                // In a real scenario, this part requires careful casting/parsing.
//                var keywords = GetKeywordsFromFilter(filter);

//                if (keywords.Any())
//                {
//                    return chunks.Where(chunk =>
//                    {
//                        var contentKeywords = SearchHelper.ExtractKeywords(chunk.Content ?? string.Empty);
//                        // Simple OR condition (does the chunk contain any query keyword?)
//                        return contentKeywords.Intersect(keywords).Any();
//                    }).ToList();
//                }
//            }

//            return chunks; // No filter applied
//        }

//        private List<string> GetKeywordsFromFilter(object filter)
//        {
//            // Simplistic implementation: Requires reflection or casting based on the anonymous type.
//            // For a robust implementation, the filter must be strongly typed (e.g., using Qdrant.Client.Grpc.Filter).

//            // Since the VectorStore created a list of { Match = { Key = ContentPayloadField, Text = k } }
//            // We can't easily parse an anonymous type here without more advanced setup.
//            // Assuming we are only checking the `content` field.

//            // For a practical in-memory scenario, we'll return a fixed list of keywords for demonstration
//            // until we introduce strong typing for the filter object.

//            // TODO: Refactor filter parameter to use a strongly-typed structure.

//            // For now, let's just return an empty list or simplify the extraction
//            // We will rely on the VectorStore.HybridSearchAsync logic being simple enough.
//            // Since the old InMemoryVectorStore calculated keywords inside its HybridSearch, 
//            // we must rely on that logic if we don't refactor the filter object.
//            return new List<string>();
//        }

//        public Task DeleteCollectionAsync(string collectionName)
//        {
//            // In-Memory: Remove from dictionary
//            _inMemoryCollections.Remove(collectionName);

//            // File: Delete the file
//            string fileName = $"{collectionName}.json";
//            string filePath = Path.Combine(_storagePath, fileName);
//            if (File.Exists(filePath))
//            {
//                File.Delete(filePath);
//            }
//            return Task.CompletedTask;
//        }
//    }
//}