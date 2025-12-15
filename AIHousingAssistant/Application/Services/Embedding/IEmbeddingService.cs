using AIHousingAssistant.Application.Enum;
using AIHousingAssistant.Models;

namespace AIHousingAssistant.Application.Services.Embedding
{
    public interface IEmbeddingService
    {
        public  Task<float[]?> EmbedAsync(string text, EmbeddingModel embeddingModel);
        public Task<List<VectorChunk>> GenerateEmbeddingsAsync(List<TextChunk> chunks, EmbeddingModel embeddingModel);
    }
}
