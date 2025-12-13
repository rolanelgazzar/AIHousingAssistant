using System;
using Microsoft.Extensions.VectorData;

namespace AIHousingAssistant.Models
{
    /// <summary>
    /// Qdrant record schema used by Semantic Kernel vector store connector.
    /// </summary>
    // Note: No class-level attribute is needed here.
    public class RagChunkRecord
    {
        // Key of the record in Qdrant (string is supported by the connector docs)
        [VectorStoreKey]
        public string Id { get; set; } = default!;

        // The actual text content of the chunk.
        [VectorStoreData(IsFullTextIndexed = true)]
        public string Content { get; set; } = string.Empty;

        // Optional source (file name, section name, etc.)
        [VectorStoreData(IsIndexed = true)]
        public string Source { get; set; } = string.Empty;

        // Vector embedding (single unnamed vector, cosine distance, HNSW index).
        // ⚠️ IMPORTANT: Change 1024 to match your embedding model dimensions.
        [VectorStoreVector(
            Dimensions: 1024,
            DistanceFunction = DistanceFunction.CosineSimilarity,
            IndexKind = IndexKind.Hnsw)]
        public float[] Embedding { get; set; } = Array.Empty<float>();
    }
}
