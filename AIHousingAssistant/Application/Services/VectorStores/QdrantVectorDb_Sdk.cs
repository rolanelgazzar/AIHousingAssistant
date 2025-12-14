using System.Collections.Generic;
using System.Threading.Tasks;
using AIHousingAssistant.Application.Services.Interfaces;
using AIHousingAssistant.Models;
using AIHousingAssistant.Models.Settings;
using DocumentFormat.OpenXml.Drawing.Charts;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;


namespace AIHousingAssistant.Application.Services.VectorDb
{
    public class QdrantVectorDb_Sdk : IVectorDB
    {
        private readonly ProviderSettings _providerSettings;
        private readonly QdrantClient _qdrantClient;


        public QdrantVectorDb_Sdk(IOptions<ProviderSettings> providerSettings)
        {
            _providerSettings = providerSettings.Value;

            _qdrantClient = new QdrantClient(_providerSettings.QDrant.Endpoint);

        }

        public async Task<bool> IsCollectionExistedAsync(string collectionName)
        {
            var collections = await _qdrantClient.ListCollectionsAsync();
            return collections != null && collections.Contains(collectionName);
        }


        public async Task<List<string>> ListAllCollectionsAsync()
        {
            // TODO: return list of collection names
            return new List<string>();
        }

        public async Task<Dictionary<string, object>> GetCollectionInfoAsync(string collectionName)
        {
            // TODO: return collection metadata
            return new Dictionary<string, object>();
        }

        public async Task<bool> DeleteCollectionAsync(string collectionName)
        {
            // TODO: delete collection if exists
            return false;
        }

        public async Task EnsureCollectionAsync(string collectionName, int vectorSize)
        {
            if (string.IsNullOrWhiteSpace(collectionName))
                throw new ArgumentException("Collection name is required.", nameof(collectionName));

            if (vectorSize <= 0)
                throw new ArgumentException("Vector size must be > 0.", nameof(vectorSize));

            // check collection
            var exists = await IsCollectionExistedAsync(collectionName);

            if (exists)
            {
                // if exists, delete
                await _qdrantClient.DeleteCollectionAsync(collectionName);
            }

            // create new one
            await _qdrantClient.CreateCollectionAsync(
                collectionName: collectionName,
                vectorsConfig: new VectorParams
                {
                    Size = (uint)vectorSize,
                    Distance = Distance.Cosine
                }
            );
        }


        public async Task UpsertAsync(string collectionName, List<VectorChunk> vectors)
        {
            if (string.IsNullOrWhiteSpace(collectionName))
                throw new ArgumentException("Collection name is required.", nameof(collectionName));

            if (vectors == null || vectors.Count == 0)
                return;

            var points = vectors
                .Where(v => v.Embedding != null && v.Embedding.Length > 0)
                .Select(v =>
                {
                    var id = (ulong)v.Index;

                    var point = new PointStruct
                    {
                        Id = new PointId { Num = id },
                        Vectors = v.Embedding
                    };

                    // Payload is read-only in your SDK version → add values instead of assigning
                    point.Payload.Add("content", new Value { StringValue = v.Content ?? string.Empty });
                    point.Payload.Add("source", new Value { StringValue = v.Source ?? string.Empty });
                    point.Payload.Add("index", new Value { IntegerValue = v.Index });

                    return point;
                })
                .ToList();

            if (!points.Any())
                return;

            await _qdrantClient.UpsertAsync(collectionName, points);
        }
        public async Task<VectorChunk?> SearchVectorAsync(string collectionName, float[] queryVector)
        {
            if (string.IsNullOrWhiteSpace(collectionName))
                throw new ArgumentException("Collection name is required.", nameof(collectionName));

            if (queryVector == null || queryVector.Length == 0)
                return null;

            var results = await _qdrantClient.SearchAsync(
             collectionName,
             queryVector,
             null,                                   // Filter?
             null,                                   // SearchParams?
             1,                                      // limit
             0UL,                                    // offset
             new WithPayloadSelector { Enable = true },
             new WithVectorsSelector { Enable = true }
         );



            var best = results?.FirstOrDefault();
            if (best == null)
                return null;

            string content = string.Empty;
            string source = string.Empty;
            int index = 0;

            if (best.Payload != null)
            {
                if (best.Payload.TryGetValue("content", out var contentVal))
                    content = contentVal?.StringValue ?? string.Empty;

                if (best.Payload.TryGetValue("source", out var sourceVal))
                    source = sourceVal?.StringValue ?? string.Empty;

                if (best.Payload.TryGetValue("index", out var indexVal))
                    index = (int)(indexVal?.IntegerValue ?? 0);
            }

            return new VectorChunk
            {
                Index = index,
                Content = content,
                Source = source,
                Embedding = ExtractEmbeddingFromVectorsOutput(best.Vectors)
            };
        }

private static float[] ExtractEmbeddingFromVectorsOutput(object? vectorsOutput)
    {
        if (vectorsOutput == null)
            return Array.Empty<float>();

        // 1) Try: vectorsOutput.Vector.Data (single vector)
        var vectorProp = vectorsOutput.GetType().GetProperty("Vector");
        var vectorObj = vectorProp?.GetValue(vectorsOutput);
        var data = ExtractFloatData(vectorObj);
        if (data.Length > 0)
            return data;

        // 2) Try: vectorsOutput.Vectors (named vectors container)
        var vectorsProp = vectorsOutput.GetType().GetProperty("Vectors")
                       ?? vectorsOutput.GetType().GetProperty("Vectors_");

        var vectorsObj = vectorsProp?.GetValue(vectorsOutput);
        if (vectorsObj == null)
            return Array.Empty<float>();

        // Try: vectorsObj is Dictionary<string, Vector> or has property "Vectors"/"Vectors_"
        // We'll look for the first item and read its Data.
        // a) If it's IEnumerable<KeyValuePair<,>>
        if (vectorsObj is System.Collections.IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                // item might be KeyValuePair<string, Vector>
                var valueProp = item?.GetType().GetProperty("Value");
                var valueObj = valueProp?.GetValue(item);
                var data2 = ExtractFloatData(valueObj);
                if (data2.Length > 0)
                    return data2;
            }
        }

        // b) If it has property "Vectors" that is enumerable
        var innerVectorsProp = vectorsObj.GetType().GetProperty("Vectors")
                            ?? vectorsObj.GetType().GetProperty("Vectors_");

        var innerVectorsObj = innerVectorsProp?.GetValue(vectorsObj);
        if (innerVectorsObj is System.Collections.IEnumerable enumerable2)
        {
            foreach (var item in enumerable2)
            {
                var valueProp = item?.GetType().GetProperty("Value");
                var valueObj = valueProp?.GetValue(item);
                var data2 = ExtractFloatData(valueObj);
                if (data2.Length > 0)
                    return data2;
            }
        }

        return Array.Empty<float>();
    }

    private static float[] ExtractFloatData(object? vectorObj)
    {
        if (vectorObj == null)
            return Array.Empty<float>();

        // Try: vectorObj.Data (RepeatedField<float>)
        var dataProp = vectorObj.GetType().GetProperty("Data");
        var dataObj = dataProp?.GetValue(vectorObj);

        if (dataObj is IEnumerable<float> floats)
            return floats.ToArray();

        return Array.Empty<float>();
    }

    public Task<List<VectorChunk>> GetAllAsync(string collectionName)
        {
            throw new NotImplementedException();
        }

        public List<string> ExtractKeywords(string text)
        {
            return text
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 3)
                .Select(w => w.ToLowerInvariant())
                .Distinct()
                .ToList();
        }
        public float CosineSimilarity(float[] a, float[] b)
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

        public async Task<List<VectorChunk>> SearchTopAsync(
            string collectionName,
            float[] queryVector,
            int top = 5)
        {
            if (string.IsNullOrWhiteSpace(collectionName))
                throw new ArgumentException("Collection name is required.", nameof(collectionName));

            if (queryVector == null || queryVector.Length == 0)
                return new List<VectorChunk>();

            var results = await _qdrantClient.SearchAsync(
                collectionName,
                queryVector,
                null,                                   // Filter?
                null,                                   // SearchParams?
                (ulong)top,                                    // limit
                0UL,                                    // offset
                new WithPayloadSelector { Enable = true },
                new WithVectorsSelector { Enable = true }
            );

            if (results == null || !results.Any())
                return new List<VectorChunk>();

            var list = new List<VectorChunk>();

            foreach (var r in results)
            {
                string content = string.Empty;
                string source = string.Empty;
                int index = 0;

                if (r.Payload != null)
                {
                    if (r.Payload.TryGetValue("content", out var contentVal))
                        content = contentVal?.StringValue ?? string.Empty;

                    if (r.Payload.TryGetValue("source", out var sourceVal))
                        source = sourceVal?.StringValue ?? string.Empty;

                    if (r.Payload.TryGetValue("index", out var indexVal))
                        index = (int)(indexVal?.IntegerValue ?? 0);
                }

                list.Add(new VectorChunk
                {
                    Index = index,
                    Content = content,
                    Source = source,
                    Embedding = ExtractEmbeddingFromVectorsOutput(r.Vectors)
                });
            }

            return list;
        }



    }
}
