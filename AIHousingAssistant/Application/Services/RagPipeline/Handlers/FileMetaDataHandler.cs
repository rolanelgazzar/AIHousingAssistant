
using AIHousingAssistant.Application.Services.RagPipeline.Abstractions;
using AIHousingAssistant.Application.Services.RagPipeline.Models;

namespace AIHousingAssistant.Application.Services.RagPipeline.Handlers
{
    public class FileMetaDataHandler : RagHandlerBase
    {
        public override async Task<RagPipelineRequest> HandleAsync(RagPipelineRequest request)
        {
            if (!string.IsNullOrEmpty(request.Content))
            {
                // Detect Language based on content
                request.Language = DetectLanguage(request.Content);

                // Populate other metadata if needed (e.g., word count, processing date)
                // request.ProcessedDate = DateTime.Now;
            }

            // Move to the next handler
            return await base.HandleAsync(request);
        }

        private string DetectLanguage(string content)
        {
            // Simple check for Arabic character range
            bool hasArabic = content.Any(c => c >= 0x0600 && c <= 0x06FF);
            return hasArabic ? "Arabic" : "English";
        }
    }
}