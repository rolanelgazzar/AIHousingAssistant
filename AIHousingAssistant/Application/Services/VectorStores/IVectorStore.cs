using AIHousingAssistant.Application.Enum;
using AIHousingAssistant.Models;

namespace AIHousingAssistant.Application.Services.VectorStores
{
    public interface IVectorStore
    {
        public Task StoreTextChunksAsVectorsAsync(List<TextChunk> chunks, RagUiRequest ragUiRequest);
        public Task<VectorChunk?> VectorSearchAsync(string queryText, RagUiRequest ragUiRequest);
        public  Task<List<VectorChunk>> SemanticSearchAsync(string queryText, RagUiRequest ragUiRequest);

        public Task<List<VectorChunk>> HybridSearchAsync(string queryText, RagUiRequest ragUiRequest);

    }
}