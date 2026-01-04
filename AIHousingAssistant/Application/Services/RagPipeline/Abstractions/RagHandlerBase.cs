using AIHousingAssistant.Application.Services.RagPipeline.Models;

namespace AIHousingAssistant.Application.Services.RagPipeline.Abstractions
{
    public abstract class RagHandlerBase : IRagHandler
    {
        private IRagHandler _next;

        // English comment: Sets the next handler in the execution chain using the interface
        public IRagHandler SetNext(IRagHandler next)
        {
            _next = next;
            return next;
        }

        // English comment: Passes the request through the chain asynchronously
        public virtual async Task<RagPipelineRequest> HandleAsync(RagPipelineRequest request)
        {
            if (_next != null)
            {
                return await _next.HandleAsync(request);
            }
            return request;
        }
    }
}