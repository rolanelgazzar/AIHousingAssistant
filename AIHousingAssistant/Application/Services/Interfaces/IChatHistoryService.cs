using AIHousingAssistant.Models;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AIHousingAssistant.Application.Services.Interfaces
{
    public interface IChatHistoryService
    {

        ChatHistory GetOrCreateHistory(string sessionId);
        void AddUserMessage(string sessionId, string message);
        void AddAssistantMessage(string sessionId, string message);
        IEnumerable<string> GetAllSessionIds();
        void ClearSession(string sessionId);
        public ChatHistory? GetChatHistory(string sessionId);
    }
}
