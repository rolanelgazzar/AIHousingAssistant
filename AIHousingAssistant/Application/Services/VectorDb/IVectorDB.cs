using AIHousingAssistant.Models;

namespace AIHousingAssistant.Application.Services.VectorDb
{
    public interface IVectorDB
    {
        Task<bool> IsCollectionExistedAsync(string collectionName);
        Task<List<string>> ListAllCollectionsAsync();
        Task<Dictionary<string, object>> GetCollectionInfoAsync(string collectionName);
        Task<bool> DeleteCollectionAsync(string collectionName);
        Task EnsureCollectionAsync(string collectionName, int vectorSize);
        Task UpsertAsync(string collectionName, List<VectorChunk> vectors);
        Task<VectorChunk?> SearchClosestAsync(string collectionName, float[] queryVector);
        Task<List<VectorChunk>> GetAllAsync(string collectionName);

    }
}
