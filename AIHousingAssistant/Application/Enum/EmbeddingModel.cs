using AIHousingAssistant.Application.Enum;
using AIHousingAssistant.Models.Settings;

namespace AIHousingAssistant.Application.Enum
{
    public enum EmbeddingModel
    {
        MxbaiEmbedLarge,
        NomicEmbedText
    }
}
public static class EmbeddingModelExtensions
{
    public static string MapToModelId(this EmbeddingModel model, EmbeddingModelSettings settings)
    {
        // Try map first using enum name as key
        if (settings?.EmbeddingModelMap != null &&
            settings.EmbeddingModelMap.TryGetValue(model.ToString(), out var mapped) &&
            !string.IsNullOrWhiteSpace(mapped))
        {
            return mapped;
        }

        // Fallback to Default from appsettings
        if (!string.IsNullOrWhiteSpace(settings?.DefaultModel))
            return settings.DefaultModel;

        // Final fallback
        return "nomic-embed-text";
    }
}