using AIHousingAssistant.Models;

namespace AIHousingAssistant.Application.Services.Interfaces
{
    public interface IVectorStore
    {
        Task StoreTextChunksAsVectorsAsync(List<TextChunk> chunks);
        public Task<VectorChunk?> SearchClosest(string queryText);
        public Task<List<VectorChunk>> GetAllAsync();
    }
}