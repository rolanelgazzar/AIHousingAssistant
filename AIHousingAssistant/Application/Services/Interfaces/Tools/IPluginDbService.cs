using AIHousingAssistant.Models;

namespace AIHousingAssistant.Application.Services.Interfaces.Tools
{
    public interface IPluginDbService
    {
        public Task<RagAnswerResponse> AskPluginDBAsync(RagUiRequest ragRequest);

    }
}
