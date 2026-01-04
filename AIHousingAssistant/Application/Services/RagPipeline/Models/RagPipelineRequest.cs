using AIHousingAssistant.Models;
using AIHousingAssistant.Models.Settings;

namespace AIHousingAssistant.Application.Services.RagPipeline.Models
{
    public class RagPipelineRequest
    {
        // Text being processed through the chain
        public string Content { get; set; } //
        public string FilePath { get; set; } //
        public string Language { get; set; } //
        public string Extension { get; set; } //
        public string FinalSavedPath { get; set; } //

        // Settings and UI Preferences
        public Settings Settings { get; set; } //
        public RagUiRequest RagUiRequest { get; set; } // Passed from UI to control Chunking/Embedding

        // RAG Output
        public List<TextChunk> Chunks { get; set; } = new List<TextChunk>(); // The result of the chunking handler
    }
}
