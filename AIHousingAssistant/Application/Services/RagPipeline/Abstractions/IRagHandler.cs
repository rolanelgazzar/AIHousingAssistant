

using AIHousingAssistant.Application.Services.RagPipeline.Models;

namespace AIHousingAssistant.Application.Services.RagPipeline.Abstractions
{
    public interface IRagHandler
    {
        // Sets the next step in the RAG pipeline
        IRagHandler SetNext(IRagHandler next);

        // Executes the logic for the current step using the unified request model
        Task<RagPipelineRequest> HandleAsync(RagPipelineRequest request);
    }
}