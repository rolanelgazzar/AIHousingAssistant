using AIHousingAssistant.Application.Services.DocumentProcessing.Abstractions;
using AIHousingAssistant.Application.Services.DocumentProcessing.Models;

namespace AIHousingAssistant.Application.Services.DocumentProcessing.Handlers
{
    public class FileMetaDataHandler : DocumentHandlerBase
    {
        public override async Task<DocumentProcessingRequest> HandleAsync(DocumentProcessingRequest request)
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