using System.Collections.Generic;
using System.Threading.Tasks;
using AIHousingAssistant.Models;
using AIHousingAssistant.Application.Enum;

namespace AIHousingAssistant.Application.Services.Interfaces
{
    public interface IVectorDB
    {
        // ------------------------- COLLECTION MANAGEMENT -------------------------
        Task<bool> IsCollectionExistedAsync(string collectionName);
        Task<List<string>> ListAllCollectionsAsync();
        Task<bool> DeleteCollectionAsync(string collectionName);
        Task<bool> CreateCollectionAsync(string collectionName, int vectorSize, QdrantDistance distance = QdrantDistance.Cosine);
        Task EnsureCollectionAsync(string collectionName, int vectorSize, QdrantDistance distance = QdrantDistance.Cosine);

        // ------------------------- DATA & VECTOR OPERATIONS -------------------------
        Task UpsertAsync(string collectionName, List<VectorChunk> vectors);

        /// <summary>
        /// Core Search Function: Handles all types of search (Vector/Semantic/Hybrid)
        /// </summary>
        Task<List<VectorChunk>> SearchAsync(
            string collectionName,
            float[] queryVector,
            int top = 3,
            object? filter = null,
            bool withPayload = true);
    }
}
