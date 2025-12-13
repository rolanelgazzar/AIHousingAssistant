namespace AIHousingAssistant.Application.Services.Interfaces
{
    public interface IEmbeddingService
    {
        Task<float[]> EmbedAsync(string text);

    }
}
