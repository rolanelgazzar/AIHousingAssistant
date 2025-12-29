using AIHousingAssistant.Models;

namespace AIHousingAssistant.Application.Services.ChatTools.Interfaces
{
    public interface IPluginDbService
    {
        public Task<RagAnswerResponse> AskPluginDBAsync(RagUiRequest ragRequest);

    }
}
