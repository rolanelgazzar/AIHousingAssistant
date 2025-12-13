using AIHousingAssistant.Models;

namespace AIHousingAssistant.Application.Services.VectorStores
{
    public interface IVectorStore
    {
        Task StoreTextChunksAsVectorsAsync(List<TextChunk> chunks);
        public Task<VectorChunk?> SearchClosest(string queryText);
        public Task<List<VectorChunk>> GetAllAsync();
    }
}