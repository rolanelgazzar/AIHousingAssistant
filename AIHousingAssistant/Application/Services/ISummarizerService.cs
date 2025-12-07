using Microsoft.SemanticKernel.ChatCompletion;

namespace AIHousingAssistant.Application.Services
{
    public interface ISummarizerService
    {
        public  Task<string> SummarizeChatAsync(ChatHistory messages);

    }
}
