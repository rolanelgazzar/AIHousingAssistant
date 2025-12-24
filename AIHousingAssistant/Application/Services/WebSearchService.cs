using AIHousingAssistant.Application.Services.Interfaces.Tools;
using AIHousingAssistant.Models;

namespace AIHousingAssistant.Application.Services
{
    public class WebSearchService : IWebSearchService
    {
        public Task<RagAnswerResponse> AskWebAsync(RagUiRequest ragRequest)
        {
            throw new NotImplementedException();
        }
    }
}
