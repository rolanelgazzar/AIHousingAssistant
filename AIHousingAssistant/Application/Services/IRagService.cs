namespace AIHousingAssistant.Application.Services
{
    public interface IRagService
    {
        Task ProcessDocumentAsync(string filePath);

    }
}
