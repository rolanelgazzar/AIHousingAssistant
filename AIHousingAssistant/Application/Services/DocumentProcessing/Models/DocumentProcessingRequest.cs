using AIHousingAssistant.Models.Settings;

namespace AIHousingAssistant.Application.Services.DocumentProcessing.Models
{
    public class DocumentProcessingRequest
    {
        public string Content { get; set; } // The text content being processed
        public string FilePath { get; set; } // Original file location
        public string Language { get; set; } // Arabic or English
        public string Extension { get; set; } // .pdf, .docx, etc.
        public string FinalSavedPath { get; set; }
        public Settings Settings { get; set; }
    }
}
