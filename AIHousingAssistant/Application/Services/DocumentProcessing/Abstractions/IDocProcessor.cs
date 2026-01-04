using AIHousingAssistant.Application.Services.DocumentProcessing.Models;

namespace AIHousingAssistant.Application.Services.DocumentProcessing.Abstractions
{
    public interface IDocProcessor
    {
        // Changed to take IFormFile as we decided in the previous step
        Task<DocumentProcessingRequest> ProcessAndSaveAsync(IFormFile file);
    }
}