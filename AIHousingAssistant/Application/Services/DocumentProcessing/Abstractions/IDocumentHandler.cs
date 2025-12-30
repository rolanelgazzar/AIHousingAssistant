using AIHousingAssistant.Application.Services.DocumentProcessing.Models;

namespace AIHousingAssistant.Application.Services.DocumentProcessing.Abstractions
{
    public interface IDocumentHandler
    {
        IDocumentHandler SetNext(IDocumentHandler next);
        Task<DocumentProcessingRequest> HandleAsync(DocumentProcessingRequest request);
    }
}