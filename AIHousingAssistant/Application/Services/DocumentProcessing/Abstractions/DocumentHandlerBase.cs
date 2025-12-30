using AIHousingAssistant.Application.Services.DocumentProcessing.Models;

namespace AIHousingAssistant.Application.Services.DocumentProcessing.Abstractions
{
    public abstract class DocumentHandlerBase : IDocumentHandler
    {
        private IDocumentHandler _nextHandler;

        public IDocumentHandler SetNext(IDocumentHandler handler)
        {
            _nextHandler = handler;
            return handler;
        }

        public virtual async Task<DocumentProcessingRequest> HandleAsync(DocumentProcessingRequest request)
        {
            if (_nextHandler != null)
            {
                return await _nextHandler.HandleAsync(request);
            }
            return request;
        }
    }
}