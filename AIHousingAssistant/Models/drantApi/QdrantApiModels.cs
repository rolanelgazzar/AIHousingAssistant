//// 1. Models/QdrantApi/QdrantResponseBase.cs
//using Qdrant.Client.Grpc;
//using System.Text.Json.Serialization;

//namespace AIHousingAssistant.Models.QdrantApi
//{
//    /// <summary>
//    /// Base class for general Qdrant API responses, containing the operation status.
//    /// </summary>
//    public class QdrantResponseBase
//    {
//        [System.Text.Json.Serialization.JsonPropertyName("status")]
//        public string Status { get; set; } // e.g., "ok" or "error"

//        [System.Text.Json.Serialization.JsonPropertyName("result")]
//        // The result structure varies widely; often used as a base for responses where TResponse
//        // will handle specific result deserialization.
//        public object? Result { get; set; }
//    }

//    /// <summary>
//    /// Represents a single point structure to be inserted or updated in Qdrant.
//    /// </summary>
//    public class UpsertPoint
//    {
//        [System.Text.Json.Serialization.JsonPropertyName("id")]
//        public ulong Id { get; set; } // Unique Point ID (must be specified for upsert)

//        [System.Text.Json.Serialization.JsonPropertyName("vector")]
//        public float[] Vector { get; set; } // The actual embedding vector

//        [System.Text.Json.Serialization.JsonPropertyName("payload")]
//        // The metadata (key-value dictionary) associated with the vector.
//        public Dictionary<string, object> Payload { get; set; } = new Dictionary<string, object>();
//    }

//    /// <summary>
//    /// The request body structure for bulk insertion or update of points (vectors) via Qdrant API.
//    /// HTTP Endpoint: PUT /collections/{collectionName}/points
//    /// </summary>
//    public class UpsertPointsRequest
//    {
//        [System.Text.Json.Serialization.JsonPropertyName("points")]
//        // The JsonPropertyName ensures the output JSON uses lowercase "points" as required by the Qdrant API.
//        public List<UpsertPoint> Points { get; set; } = new List<UpsertPoint>();
//    }

//    public class CollectionInfo
//    {
//        [System.Text.Json.Serialization.JsonPropertyName("name")]
//        public string Name { get; set; } = string.Empty;
//    }

//    // Models/QdrantApi/ListCollectionsResult.cs
//    public class ListCollectionsResult
//    {
//        [System.Text.Json.Serialization.JsonPropertyName("collections")]
//        public List<CollectionInfo> Collections { get; set; } = new List<CollectionInfo>();
//    }

//    // Models/QdrantApi/ListCollectionsResponse.cs (Base Response with specific Result type)
//    public class ListCollectionsResponse : QdrantResponseBase
//    {
//        // The base class QdrantResponseBase has 'result', but using generics/inheritance 
//        // helps in direct deserialization if using a more advanced deserializer.
//        // For simplicity with System.Text.Json, we will parse the object? Result field.
//        // A clean approach is often defining the full response structure:
//        [System.Text.Json.Serialization.JsonPropertyName("result")]
//        public ListCollectionsResult? Result { get; set; }
//    }
//    /// <summary>
//    /// Defines the parameters for the vectors in the collection (size and distance metric).
//    /// </summary>
//    public class QdrantVectorParams
//    {
//        [System.Text.Json.Serialization.JsonPropertyName("size")]
//        // The dimension size of the vectors (e.g., 1536 for OpenAI embeddings)
//        public uint Size { get; set; }

//        [System.Text.Json.Serialization.JsonPropertyName("distance")]
//        // The distance metric (e.g., "Cosine", "Dot", "Euclid")
//        public string Distance { get; set; } = "Cosine";

//        // Note: You can add other vector options here if needed, like 'on_disk' or 'hnsw_config'.
//    }
//    /// <summary>
//    /// The request body structure for creating a new collection in Qdrant.
//    /// HTTP Endpoint: PUT /collections/{collectionName}
//    /// </summary>
//    public class CreateCollectionRequest
//    {
//        [System.Text.Json.Serialization.JsonPropertyName("vectors")]
//        // Defines the vector index parameters for the collection.
//        public QdrantVectorParams Vectors { get; set; } = new QdrantVectorParams();

//        // Note: You can add other configurations here, like 'replication_factor' or 'optimizers_config'.
//    }

//    public class SearchRequest
//    {
//        [System.Text.Json.Serialization.JsonPropertyName("vector")]
//        public float[] Vector { get; set; } = Array.Empty<float>();

//        [System.Text.Json.Serialization.JsonPropertyName("limit")]
//        public int Limit { get; set; }

//        [System.Text.Json.Serialization.JsonPropertyName("with_payload")]
//        public bool WithPayload { get; set; } = true; // Crucial to get the source content
//        public Filter? Filter { get; set; }

//    }

//    // 2. SearchResultItem.cs
//    public class SearchResultItem
//    {
//        [System.Text.Json.Serialization.JsonPropertyName("id")]
//        public ulong Id { get; set; }

//        [System.Text.Json.Serialization.JsonPropertyName("score")]
//        public float Score { get; set; }

//        [System.Text.Json.Serialization.JsonPropertyName("payload")]
//        public Dictionary<string, object> Payload { get; set; } = new Dictionary<string, object>();
//    }

//    // 3. SearchResponse.cs (inherits from QdrantResponseBase or just mirrors its structure)
//    public class SearchResponse : QdrantResponseBase
//    {
//        [System.Text.Json.Serialization.JsonPropertyName("result")]
//        public List<SearchResultItem>? Result { get; set; }
//    }

//    public class Filter
//    {
//        public List<FieldCondition>? Must { get; set; }
//        public List<FieldCondition>? Should { get; set; }

//        [JsonPropertyName("must_not")]
//        public List<FieldCondition>? MustNot { get; set; }
//    }
//    public class FieldCondition
//    {
//        public string Key { get; set; } = default!;
//        public Match? Match { get; set; }
//        public Range? Range { get; set; }
//    }
//    public class Match
//    {
//        public object? Value { get; set; }
//    }
//    public class Range
//    {
//        public double? Gt { get; set; }
//        public double? Gte { get; set; }
//        public double? Lt { get; set; }
//        public double? Lte { get; set; }
//    }
  
//}