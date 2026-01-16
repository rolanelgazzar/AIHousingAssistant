using AIHousingAssistant.Application.Enum;
using AIHousingAssistant.Models.Settings;

namespace AIHousingAssistant.Application.Enum
{
    public enum EmbeddingModel
    {
        MxbaiEmbedLarge,
        NomicEmbedText,
        BgeM3
    }
}
public static class EmbeddingModelExtensionsa
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


    public static int GetMaxTokenLimit(this EmbeddingModel model)
    {
        return model switch
        {
            // Mxbai: 512 tokens
            EmbeddingModel.MxbaiEmbedLarge => 512,

            // NomicEmbedText: 8192 tokens
            EmbeddingModel.NomicEmbedText => 8192,

            // BGE-M3: 8192 tokens
            EmbeddingModel.BgeM3 => 8192,

            // Default fallback
            _ => 512
        };
    }

    /// <summary>
    /// Estimates token count for English and Arabic text.
    /// Uses a slightly different char-per-token factor for Arabic (~2.5) 
    /// vs English (~3.5) to better approximate actual tokenization.
    /// </summary>
    public static int EstimateTokenCount(this EmbeddingModel model, string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;

        // Simple heuristic: if more than 30% characters are Arabic, use Arabic factor
        int arabicChars = text.Count(c => (c >= 0x0600 && c <= 0x06FF) || (c >= 0x0750 && c <= 0x077F));
        double arabicRatio = (double)arabicChars / text.Length;

        double charsPerToken = arabicRatio > 0.3 ? 2.5 : 3.5;

        return (int)Math.Ceiling(text.Length / charsPerToken);
    }

    /// <summary>
    /// Returns a safe chunk size in characters for the model.
    /// Uses 80% of max token limit as buffer.
    /// Supports Arabic and English text reasonably well.
    /// </summary>
    public static int GetSafeChunkSize(this EmbeddingModel model, string sampleText = null)
    {
        int maxTokens = model.GetMaxTokenLimit();
        int safeTokens = (int)(maxTokens * 0.8); // leave 20% buffer

        double charsPerToken = 3.5; // default English
        if (!string.IsNullOrEmpty(sampleText))
        {
            int arabicChars = sampleText.Count(c => (c >= 0x0600 && c <= 0x06FF) || (c >= 0x0750 && c <= 0x077F));
            double arabicRatio = (double)arabicChars / sampleText.Length;
            if (arabicRatio > 0.3) charsPerToken = 2.5;
        }

        return (int)(safeTokens * charsPerToken);
    }
    //-----------------------------------------------------------------
    //public static int GetMaxTokenLimit(this EmbeddingModel model)
    //{
    //    return model switch
    //    {
    //        // Mxbai: 512 tokens
    //        EmbeddingModel.MxbaiEmbedLarge => 512,

    //        // English comment: Nomic fails with larger context in some Ollama setups.
    //        // Capping at 512 ensures 100% compatibility and higher precision for RAG.
    //        EmbeddingModel.NomicEmbedText => 512,

    //        // BGE-M3 is more robust, but 1024 is a safe middle ground.
    //        EmbeddingModel.BgeM3 => 1024,

    //        _ => 512
    //    };
    //}

    //public static int EstimateTokenCount(this EmbeddingModel model, string text)
    //{
    //    if (string.IsNullOrEmpty(text)) return 0;

    //    int arabicChars = text.Count(c => (c >= 0x0600 && c <= 0x06FF));
    //    double arabicRatio = (double)arabicChars / text.Length;

    //    // English comment: Arabic is very token-heavy in Nomic. 
    //    // We use 1.2 for Arabic and 3.5 for English to be extra safe.
    //    double charsPerToken = arabicRatio > 0.3 ? 1.2 : 3.5;

    //    return (int)Math.Ceiling(text.Length / charsPerToken);
    //}

    //public static int GetSafeChunkSize(this EmbeddingModel model, string sampleText = null)
    //{
    //    int maxTokens = model.GetMaxTokenLimit();

    //    // English comment: Use 80% of the capped limit.
    //    int safeTokens = (int)(maxTokens * 0.8);

    //    double charsPerToken = 3.5;
    //    if (!string.IsNullOrEmpty(sampleText))
    //    {
    //        int arabicChars = sampleText.Count(c => (c >= 0x0600 && c <= 0x06FF));
    //        double arabicRatio = (double)arabicChars / sampleText.Length;

    //        // English comment: Adjust factor for Arabic text to prevent overflow.
    //        if (arabicRatio > 0.3) charsPerToken = 1.2;
    //    }

    //    return (int)(safeTokens * charsPerToken);
    //}

}



