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

    //public static int GetSafeChunkSize(this EmbeddingModel model, string sampleText = null)
    //{
    //    //512 tokens
    //    int maxTokens = model.GetMaxTokenLimit();

    //    // English comment: Reduce chunk size to avoid hitting TPD (Tokens Per Day) limits.
    //    // Instead of using 80% of 8192 (which is too large), we cap it at a reasonable size like 400-500 tokens.
    //    int effectiveLimit = Math.Min(maxTokens, 512);
    //    int safeTokens = (int)(effectiveLimit * 0.9); // 10% buffer is enough for small chunks

    //    double charsPerToken = 3.5;
    //    if (!string.IsNullOrEmpty(sampleText))
    //    {
    //        int arabicChars = sampleText.Count(c => (c >= 0x0600 && c <= 0x06FF) || (c >= 0x0750 && c <= 0x077F));
    //        double arabicRatio = (double)arabicChars / sampleText.Length;
    //        if (arabicRatio > 0.3) charsPerToken = 2.0; // English comment: Arabic tokens are often heavier (fewer chars per token)
    //    }

    //    return (int)(safeTokens * charsPerToken);
    //}
}



