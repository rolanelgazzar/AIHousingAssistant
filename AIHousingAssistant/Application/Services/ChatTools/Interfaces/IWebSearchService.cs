using AIHousingAssistant.Models;

namespace AIHousingAssistant.Application.Services.ChatTools.Interfaces
{
    public interface IWebSearchService
    {
        public Task<RagAnswerResponse> AskWebAsync(RagUiRequest ragRequest);
        public Task<RagAnswerResponse> askPluginWebAsync(RagUiRequest ragRequest);

    }
}
