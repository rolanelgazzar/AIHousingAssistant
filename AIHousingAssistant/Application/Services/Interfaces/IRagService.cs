namespace AIHousingAssistant.Application.Services.Interfaces
{
    public interface IRagService
    {
        public Task ProcessDocumentAsync(IFormFile file);
        public Task<string> AskRagJsonAsync(string query);
        public Task<string> AskRagQDrantAsync(string query);

    }
}
