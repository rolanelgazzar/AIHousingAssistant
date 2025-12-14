using AIHousingAssistant.Application.Enum;
using AIHousingAssistant.Models;

namespace AIHousingAssistant.Application.Services.VectorStores
{
    public interface IVectorStore
    {
        Task StoreTextChunksAsVectorsAsync(List<TextChunk> chunks, EmbeddingModel embeddingModel);
        public Task<VectorChunk?> VectorSearchAsync(string queryText);
        public Task<List<VectorChunk>> HybridSearchAsync(string queryText,int top =5);

        public Task<List<VectorChunk>> SemanticSearchAsync(string queryText, int top = 5);

        public Task<List<VectorChunk>> GetAllAsync();
    }
}