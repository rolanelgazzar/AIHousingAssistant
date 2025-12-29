using AIHousingAssistant.Models;

namespace AIHousingAssistant.Application.Services.ChatTools.Interfaces
{
    public interface IDirectChatService
    {
        public Task<RagAnswerResponse> AskDirectChatAsync(RagUiRequest ragRequest);

    }
}
