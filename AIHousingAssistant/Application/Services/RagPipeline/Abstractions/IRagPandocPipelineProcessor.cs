// AIHousingAssistant.Application.Services.RagPipeline.Abstractions/IRagPipelineProcessor.cs
using AIHousingAssistant.Application.Services.RagPipeline.Models;
using AIHousingAssistant.Models;

namespace AIHousingAssistant.Application.Services.RagPipeline.Abstractions
{
    public interface IRagPandocPipelineProcessor
    {
        // English comment: Orchestrates the entire RAG flow from file upload to vector generation
        Task<RagPipelineRequest> ExecutePipelineAsync(IFormFile file, RagUiRequest uiRequest);
    }
}