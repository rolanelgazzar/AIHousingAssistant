using Microsoft.SemanticKernel.ChatCompletion;

namespace AIHousingAssistant.Application.Services.Interfaces
{
    public interface ISummarizerService
    {
        public Task<string> SummarizeChatAsync(ChatHistory messages);

    }
}
