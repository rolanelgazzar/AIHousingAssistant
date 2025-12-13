using AIHousingAssistant.Application.Enum;
using AIHousingAssistant.Models.Settings;
using Microsoft.Extensions.Options;
using OllamaSharp;

namespace AIHousingAssistant.Application.Services.Embedding
{
    public class EmbeddingService : IEmbeddingService
    {
        private readonly OllamaApiClient _client;
        private readonly ProviderSettings _providerSettings;

        public EmbeddingService(IOptions<ProviderSettings> providerSettings)
        {
            var settings = providerSettings.Value;

            _client = new OllamaApiClient(settings.OllamaEmbedding.Endpoint);
            _client.SelectedModel = settings.OllamaEmbedding.Model;
        }

        public async Task<float[]> EmbedAsync(string text, EmbeddingModel embeddingModel = EmbeddingModel.NomicEmbedText)
        {
            if (string.IsNullOrWhiteSpace(text))
                return Array.Empty<float>();

            text = text.Trim().ToLowerInvariant();

            var modelId = embeddingModel.MapToModelId(_providerSettings.OllamaEmbedding);

            _client.SelectedModel = modelId;

            var response = await _client.EmbedAsync(text);
            if (response.Embeddings != null && response.Embeddings.Count > 0)
            {
                var embedding = response.Embeddings[0];
                return embedding.Length > 0 ? embedding : Array.Empty<float>();
            }

            return Array.Empty<float>();
        }
    }
}
