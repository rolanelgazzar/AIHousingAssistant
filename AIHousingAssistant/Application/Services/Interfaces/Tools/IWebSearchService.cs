using AIHousingAssistant.Models;

namespace AIHousingAssistant.Application.Services.Interfaces.Tools
{
    public interface IWebSearchService
    {
        public Task<RagAnswerResponse> AskWebAsync(RagUiRequest ragRequest);
        public Task<RagAnswerResponse> askPluginWebAsync(RagUiRequest ragRequest);

    }
}
