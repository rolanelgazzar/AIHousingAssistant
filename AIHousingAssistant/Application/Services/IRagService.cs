namespace AIHousingAssistant.Application.Services
{
    public interface IRagService
    {
        public Task ProcessDocumentAsync(IFormFile file);
        public Task<string> AskRagAsync(string query);

    }
}
