using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AIHousingAssistant.Application.Services.Interfaces;
using AIHousingAssistant.Models;
using AIHousingAssistant.Models.Settings;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using AIHousingAssistant.Application.Enum;
using AIHousingAssistant.Helper;

namespace AIHousingAssistant.Application.Services.VectorDb
{
    /// <summary>
    /// QdrantVectorDb_Sdk implementation using NuGet SDK.
    /// Implements IVectorDB interface only.
    /// </summary>
    public class QdrantVectorDb_Sdk : IVectorDB
    {
        private readonly ProviderSettings _providerSettings;
        private readonly QdrantClient _qdrantClient;

        /// <summary>
        /// Constructor: initializes Qdrant client with endpoint from settings.
        /// </summary>
        public QdrantVectorDb_Sdk(IOptions<ProviderSettings> providerSettings)
        {
            _providerSettings = providerSettings.Value;
            _qdrantClient = new QdrantClient(_providerSettings.QDrant.Endpoint);
        }

        // ------------------------- COMMON VALIDATION -------------------------
        private void ValidateCollectionName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Collection name is required.", nameof(name));
        }

        private void ValidateQueryVector(float[] vector)
        {
            if (vector == null || vector.Length == 0)
                throw new ArgumentException("Query vector cannot be null or empty.");
        }

        // ------------------------- COLLECTION MANAGEMENT -------------------------

        /// <summary>
        /// Check if collection exists in Qdrant.
        /// </summary>
        public async Task<bool> IsCollectionExistedAsync(string collectionName)
        {
            ValidateCollectionName(collectionName);

            var collections = await _qdrantClient.ListCollectionsAsync();
            if (collections == null) return false;

            return collections.Any(c => string.Equals(c, collectionName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// List all collection names from Qdrant.
        /// </summary>
        public async Task<List<string>> ListAllCollectionsAsync()
        {
            var collections = await _qdrantClient.ListCollectionsAsync();
            return collections?.ToList() ?? new List<string>();
        }

        /// <summary>
        /// Delete a collection in Qdrant.
        /// </summary>
        public async Task<bool> DeleteCollectionAsync(string collectionName)
        {
            ValidateCollectionName(collectionName);

            try
            {
                await _qdrantClient.DeleteCollectionAsync(collectionName);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Create a new collection with specified vector size and distance metric.
        /// </summary>
        public async Task<bool> CreateCollectionAsync(string collectionName, int vectorSize, QdrantDistance distance = QdrantDistance.Cosine)
        {
            ValidateCollectionName(collectionName);
            if (vectorSize <= 0)
                throw new ArgumentException("Vector size must be greater than zero.", nameof(vectorSize));

            await _qdrantClient.CreateCollectionAsync(
                collectionName,
                new VectorParams
                {
                    Size = (uint)vectorSize,
                    Distance = distance == QdrantDistance.Cosine ? Distance.Cosine : Distance.Euclid
                });

            return true;
        }

        /// <summary>
        /// Ensure collection exists: delete and recreate if exists.
        /// </summary>
        public async Task EnsureCollectionAsync(string collectionName, int vectorSize, QdrantDistance distance = QdrantDistance.Cosine)
        {
            var exists = await IsCollectionExistedAsync(collectionName);
            if (exists)
            {
                await DeleteCollectionAsync(collectionName);
            }

            await CreateCollectionAsync(collectionName, vectorSize, distance);
        }

        // ------------------------- DATA & VECTOR OPERATIONS -------------------------

        /// <summary>
        /// Upsert vector chunks into Qdrant collection.
        /// </summary>
        public async Task UpsertAsync(string collectionName, List<VectorChunk> vectors)
        {
            ValidateCollectionName(collectionName);
            if (vectors == null || vectors.Count == 0) return;

            var points = vectors
                .Where(v => v.Embedding != null && v.Embedding.Length > 0)
                .Select(v =>
                {
                    var point = new PointStruct
                    {
                        Id = new PointId { Num = (ulong)v.Index },
                        Vectors = v.Embedding
                    };

                    point.Payload.Add("content", new Value { StringValue = v.Content ?? string.Empty });
                    point.Payload.Add("source", new Value { StringValue = v.Source ?? string.Empty });
                    point.Payload.Add("index", new Value { IntegerValue = v.Index });

                    return point;
                }).ToList();

            if (!points.Any()) return;

            await _qdrantClient.UpsertAsync(collectionName, points);
        }

        /// <summary>
        /// Search vectors in collection with optional top-k limit and filter.
        /// </summary>
        /// <summary>
        /// Search vectors in collection with optional top-k limit and filter.
        /// Filter must be of type Qdrant.Client.Grpc.Filter?; pass null if no filter.
        /// </summary>
        public async Task<List<VectorChunk>> SearchAsync(
            string collectionName,
            float[] queryVector,
            int top = 3,
            object? filter = null,  // Interface defines object?, SDK needs Filter?
            bool withPayload = true)
        {
            ValidateCollectionName(collectionName);
            ValidateQueryVector(queryVector);

            // Convert object? to Filter? (if null, pass null)
            Filter? sdkFilter = filter as Filter;

            var results = await _qdrantClient.SearchAsync(
                collectionName,
                queryVector,
                sdkFilter,                              // ✅ must be Filter? type
                null,                                   // SearchParams
                (ulong)top,
                0UL,
                new WithPayloadSelector { Enable = withPayload },
                new WithVectorsSelector { Enable = true }
            );

            if (results == null || !results.Any())
                return new List<VectorChunk>();

            return results.Select(r => new VectorChunk
            {
                Index = (int)(r.Payload?["index"]?.IntegerValue ?? 0),
                Content = r.Payload?["content"]?.StringValue ?? string.Empty,
                Source = r.Payload?["source"]?.StringValue ?? string.Empty,
                Embedding = SearchHelper.ExtractEmbeddingFromVectorsOutput(r.Vectors)

            }).ToList();
        }

    }
}
