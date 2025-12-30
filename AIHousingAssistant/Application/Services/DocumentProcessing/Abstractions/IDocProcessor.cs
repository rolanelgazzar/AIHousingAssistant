namespace AIHousingAssistant.Application.Services.DocumentProcessing.Abstractions
{
    public interface IDocProcessor
    {
        // Changed to take IFormFile as we decided in the previous step
        Task<string> ProcessAndSaveAsync(IFormFile file);
    }
}