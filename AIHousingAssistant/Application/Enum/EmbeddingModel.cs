using AIHousingAssistant.Application.Enum;
using AIHousingAssistant.Models.Settings;

namespace AIHousingAssistant.Application.Enum
{
    public enum EmbeddingModel
    {
        NomicEmbedText,
        MxbaiEmbedLarge,
        BgeSmallEn,
        BgeLargeEn
    }
}
public static class EmbeddingModelExtensions
{
    public static string MapToModelId(this EmbeddingModel model, OllamaEmbeddingSettings settings)
    {
        // Try map first using enum name as key
        if (settings?.EmbeddingModelMap != null &&
            settings.EmbeddingModelMap.TryGetValue(model.ToString(), out var mapped) &&
            !string.IsNullOrWhiteSpace(mapped))
        {
            return mapped;
        }

        // Fallback to Default from appsettings
        if (!string.IsNullOrWhiteSpace(settings?.Default))
            return settings.Default;

        // Final fallback
        return "nomic-embed-text";
    }
}