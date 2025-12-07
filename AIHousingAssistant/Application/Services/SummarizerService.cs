using AIHousingAssistant.Models.Settings;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel.ChatCompletion;
using OllamaSharp;
using System.Text;

namespace AIHousingAssistant.Application.Services
{
    public class SummarizerService : ISummarizerService
    {
        private readonly OllamaApiClient _client;
        private readonly ProviderSettings _providerSettings;

        public SummarizerService(IOptions<ProviderSettings> providerSettings )
        {
            _providerSettings = providerSettings.Value;
            _client = new OllamaApiClient(_providerSettings.Ollama.Endpoint);
            _client.SelectedModel = _providerSettings.Ollama.Model ;
        }

        public async Task<string> SummarizeChatAsync(ChatHistory messages)
        {
            if (messages == null || messages.Count == 0)
                return "No history available.";

            var sb = new StringBuilder();
            foreach (var msg in messages)
            {
                sb.AppendLine($"{msg.Role}: {msg.Content}");
            }

            var prompt = $@"
Summarize the following chat conversation in a short, clear, structured paragraph:

{sb}
";

            var summaryBuilder = new StringBuilder();

            await foreach (var chunk in _client.GenerateAsync(prompt))
            {
                if (chunk?.Response != null)
                    summaryBuilder.Append(chunk.Response);
            }

            return summaryBuilder.ToString();
        }
    }
}
