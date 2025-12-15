using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using AIHousingAssistant.Application.Services.Interfaces;
using AIHousingAssistant.Models;
using AIHousingAssistant.Models.Settings;
using AIHousingAssistant.Helper;
using AIHousingAssistant.Application.Enum;

namespace AIHousingAssistant.Application.Services.VectorDb
{
    public class QdrantVectorDb_Http : IVectorDB
    {
        private readonly HttpClientHelper _httpClientHelper;
        private readonly ProviderSettings _providerSettings;
        private const string CollectionsBaseUrl = "collections";

        public QdrantVectorDb_Http(
            HttpClientHelper httpClientHelper,
            IOptions<ProviderSettings> providerSettings)
        {
            _httpClientHelper = httpClientHelper;
            _providerSettings = providerSettings.Value;
        }

        // ------------------------- COMMON VALIDATION -------------------------
        private void ValidateCollectionName(string collectionName)
        {
            if (string.IsNullOrWhiteSpace(collectionName))
                throw new ArgumentException("Collection name is required.", nameof(collectionName));
        }

        private void ValidateQueryVector(float[] queryVector)
        {
            if (queryVector == null || queryVector.Length == 0)
                throw new ArgumentException("Query vector cannot be null or empty.", nameof(queryVector));
        }

        private void ValidateTop(int top)
        {
            if (top <= 0)
                throw new ArgumentException("Top must be greater than zero.", nameof(top));
        }

        // ------------------------- COLLECTION MANAGEMENT -------------------------

        public async Task<bool> IsCollectionExistedAsync(string collectionName)
        {
            ValidateCollectionName(collectionName);

            try
            {
                var url = $"/{CollectionsBaseUrl}";
                dynamic response = await _httpClientHelper.GetAsync<dynamic>(url);

                if (response?.status != "ok" || response?.result?.collections == null)
                    return false;

                foreach (var c in response.result.collections)
                {
                    if (string.Equals((string)c.name, collectionName, StringComparison.OrdinalIgnoreCase))
                        return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in IsCollectionExistedAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<List<string>> ListAllCollectionsAsync()
        {
            try
            {
                var url = $"/{CollectionsBaseUrl}";
                dynamic response = await _httpClientHelper.GetAsync<dynamic>(url);

                var result = new List<string>();
                if (response?.status != "ok" || response?.result?.collections == null)
                    return result;

                foreach (var c in response.result.collections)
                {
                    result.Add((string)c.name);
                }

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ListAllCollectionsAsync: {ex.Message}");
                return new List<string>();
            }
        }


        public async Task<bool> DeleteCollectionAsync(string collectionName)
        {
            ValidateCollectionName(collectionName);

            try
            {
                var url = $"/{CollectionsBaseUrl}/{collectionName}?wait=true";
                await _httpClientHelper.DeleteAsync(url);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in DeleteCollectionAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> CreateCollectionAsync(string collectionName, int vectorSize, QdrantDistance distance = QdrantDistance.Cosine)
        {
            ValidateCollectionName(collectionName);

            try
            {
                var createRequest = new
                {
                    vectors = new
                    {
                        size = (uint)vectorSize,
                        distance = System.Enum.GetName(typeof(QdrantDistance), distance)
                    }
                };

                var url = $"/{CollectionsBaseUrl}/{collectionName}?timeout=30";
                dynamic response = await _httpClientHelper.PutAsync<dynamic, dynamic>(url, createRequest);

                if (response?.status != "ok")
                    throw new InvalidOperationException($"Qdrant failed to create collection '{collectionName}'.");

                Console.WriteLine($"Collection '{collectionName}' created successfully.");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CreateCollectionAsync Error: {ex.Message}");
                return false;
            }
        }

        public async Task EnsureCollectionAsync(string collectionName, int vectorSize, QdrantDistance distance = QdrantDistance.Cosine)
        {
            try
            {
                var exists = await IsCollectionExistedAsync(collectionName);
                if (exists)
                {
                    Console.WriteLine($"Collection '{collectionName}' exists. Deleting...");
                    bool deleted = await DeleteCollectionAsync(collectionName);
                    if (!deleted)
                        throw new InvalidOperationException($"Failed to delete collection '{collectionName}'.");
                }

                await CreateCollectionAsync(collectionName, vectorSize, distance);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"EnsureCollectionAsync Error: {ex.Message}");
                throw;
            }
        }

        // ------------------------- DATA & VECTOR OPERATIONS -------------------------

        public async Task UpsertAsync(string collectionName, List<VectorChunk> vectors)
        {
            ValidateCollectionName(collectionName);

            try
            {
                if (vectors == null || vectors.Count == 0)
                {
                    Console.WriteLine("Upsert called with empty vectors. Skipped.");
                    return;
                }

                var points = vectors
                    .Where(v => v.Embedding != null && v.Embedding.Length > 0 && v.Index >= 0)
                    .Select(v => new
                    {
                        id = (ulong)v.Index,
                        vector = v.Embedding,
                        payload = new
                        {
                            content = v.Content ?? "",
                            source = v.Source ?? "",
                            index = v.Index
                        }
                    }).ToList();

                if (points.Count == 0)
                {
                    Console.WriteLine("No valid points to upsert. Skipped.");
                    return;
                }

                var url = $"/{CollectionsBaseUrl}/{collectionName}/points?wait=true";
                dynamic response = await _httpClientHelper.PutAsync<dynamic, dynamic>(url, new { points });

                if (response?.status != "ok")
                    throw new InvalidOperationException($"Qdrant failed to upsert points into '{collectionName}'.");

                Console.WriteLine($"Successfully upserted {points.Count} points.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in UpsertAsync: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Core Search Function: Handles all types of search (Vector/Semantic/Hybrid)
        /// </summary>
        public async Task<List<VectorChunk>> SearchAsync(
            string collectionName,
            float[] queryVector,
            int top = 3,
            object? filter = null,
            bool withPayload = true)
        {
            ValidateCollectionName(collectionName);
            ValidateQueryVector(queryVector);
            ValidateTop(top);

            try
            {
                var request = new
                {
                    vector = queryVector,
                    limit = top,
                    with_payload = withPayload,
                    filter
                };

                var url = $"/{CollectionsBaseUrl}/{collectionName}/points/search";
                dynamic response = await _httpClientHelper.PostAsync<dynamic, dynamic>(url, request);

                if (response?.status != "ok" || response?.result == null)
                    return new List<VectorChunk>();

                var result = new List<VectorChunk>();
                foreach (var item in response.result)
                {
                    result.Add(new VectorChunk
                    {
                        Content = item.payload?.content,
                        Source = item.payload?.source,
                        Index = (int)item.id,
                        Similarity = (float)item.score
                    });
                }

                return result.Where(x => !string.IsNullOrWhiteSpace(x.Content)).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SearchAsync Error: {ex.Message}");
                return new List<VectorChunk>();
            }
        }
    }
}
