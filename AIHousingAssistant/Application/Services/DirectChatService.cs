using AIHousingAssistant.Application.Services.Interfaces.Tools;
using AIHousingAssistant.Models;

namespace AIHousingAssistant.Application.Services
{
    public class DirectChatService : IDirectChatService
    {
        public Task<RagAnswerResponse> AskDirectChatAsync(RagUiRequest ragRequest)
        {
            throw new NotImplementedException();
        }
    }
}
