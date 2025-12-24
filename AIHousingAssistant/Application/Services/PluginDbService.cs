using AIHousingAssistant.Application.Services.Interfaces.Tools;
using AIHousingAssistant.Models;

namespace AIHousingAssistant.Application.Services
{
    public class PluginDbService : IPluginDbService
    {
        public Task<RagAnswerResponse> AskPluginDBAsync(RagUiRequest ragRequest)
        {
            throw new NotImplementedException();
        }
    }
}
