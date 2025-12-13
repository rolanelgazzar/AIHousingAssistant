using AIHousingAssistant.Application.Enum;

namespace AIHousingAssistant.Application.Services.Embedding
{
    public interface IEmbeddingService
    {
        Task<float[]> EmbedAsync(string text, EmbeddingModel embeddingModel = EmbeddingModel.NomicEmbedText);

    }
}
