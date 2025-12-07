using AIHousingAssistant.Models;

namespace AIHousingAssistant.Application.Services
{
    public interface IVectorStore
    {
        Task<float[]> TextToVectorAsync(string text);
        Task StoreTextChunksAsVectorsAsync(List<TextChunk> chunks);
        public  Task<VectorChunk?> SearchClosest(String queryText);
        public Task<List<VectorChunk>> GetAllAsync();
    }
}