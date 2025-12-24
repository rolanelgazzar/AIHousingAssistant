using AIHousingAssistant.Models;

namespace AIHousingAssistant.Application.Services.Interfaces.Tools
{
    public interface IDirectChatService
    {
        public Task<RagAnswerResponse> AskDirectChatAsync(RagUiRequest ragRequest);

    }
}
