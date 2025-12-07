
using Microsoft.SemanticKernel.ChatCompletion;
using System.Collections.Concurrent;

namespace AIHousingAssistant.Application.Services
{
    
   

    public class ChatHistoryService : IChatHistoryService
    {
        
        private readonly ConcurrentDictionary<string, ChatHistory> _sessions = new();

        public ChatHistory GetOrCreateHistory(string sessionId)
        {
            return _sessions.GetOrAdd(sessionId, id =>
            {
                var history = new ChatHistory();
                history.AddSystemMessage("أنت مساعد سكن ذكي.");
                return history;
            });
        }

        public void AddUserMessage(string sessionId, string message)
        {
            var history = GetOrCreateHistory(sessionId);
            history.AddUserMessage(message);
        }

        public void AddAssistantMessage(string sessionId, string message)
        {
            var history = GetOrCreateHistory(sessionId);
            history.AddAssistantMessage(message);
        }

        public IEnumerable<string> GetAllSessionIds()
        {
            return _sessions.Keys;
        }

        public void ClearSession(string sessionId)
        {
            _sessions.TryRemove(sessionId, out _);
        }
        // ---------------------------
        // New: get chat history if exists
        // ---------------------------
        public ChatHistory? GetChatHistory(string sessionId)
        {
            _sessions.TryGetValue(sessionId, out var history);
            return history;
        }
    }
}
