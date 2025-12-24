using AIHousingAssistant.Application.SemanticKernel;
using AIHousingAssistant.Application.SemanticKernel.Plugins; // Ensure this is imported
using AIHousingAssistant.Application.Services.Interfaces;
using AIHousingAssistant.Application.Services.Interfaces.Tools;
using AIHousingAssistant.Models;
using AIHousingAssistant.Models.Settings;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Diagnostics.CodeAnalysis;
using SKGoogle = Microsoft.SemanticKernel.Plugins.Web.Google.GoogleConnector;

namespace AIHousingAssistant.Application.Services
{
    public class WebSearchService : IWebSearchService
    {
        private readonly IChatHistoryService _chatHistoryService;
        private readonly ProviderSettings _providerSettings;

        public WebSearchService(
            IChatHistoryService chatHistoryService,
            IOptions<ProviderSettings> providerSettings)
        {
            _chatHistoryService = chatHistoryService;
            _providerSettings = providerSettings.Value;
        }

        public Task<RagAnswerResponse> askPluginWebAsync(RagUiRequest ragRequest)
        {
            throw new NotImplementedException();
        }

        [UnconditionalSuppressMessage("Trimming", "IL2026")]
        public async Task<RagAnswerResponse> AskWebAsync(RagUiRequest ragRequest)
        {
            try
            {
                // 1. Build the Kernel using your helper
                var kernelBuilder = SemanticKernelHelper.BuildKernel(ragRequest.AIProvider, _providerSettings);

#pragma warning disable SKEXP0050
                // 2. Initialize the Google Connector
                var googleConnector = new SKGoogle(
                    apiKey: _providerSettings.GoogleConnector.ApiKey,
                    searchEngineId: _providerSettings.GoogleConnector.SearchEngineId
                );

                // 3. Register your custom WebSearchPlugin
                var searchPlugin = new WebSearchPlugin(googleConnector);
                kernelBuilder.Plugins.AddFromObject(searchPlugin, "WebSearchTool");
#pragma warning restore SKEXP0050

                var kernel = SemanticKernelHelper.Build(kernelBuilder);

                // 4. Setup Chat History and System Instructions
                var history = new ChatHistory();
                history.AddSystemMessage("You are a web assistant. Always use the 'WebSearchTool' for questions about current events or weather.");
                history.AddUserMessage(ragRequest.Query);

                // 5. Configure Tool Calling Behavior (The Planner logic)
                var settings = new OpenAIPromptExecutionSettings
                {
                    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                    Temperature = 0.3
                };

                // 6. Get Chat Completion Service and Execute
                var chatService = kernel.GetRequiredService<IChatCompletionService>();

                // Crucial: Pass the kernel instance to enable tool calling
                var response = await chatService.GetChatMessageContentAsync(
                    history,
                    executionSettings: settings,
                    kernel: kernel);

                string botResponse = response.Content ?? "I couldn't find an answer on the web.";

                // 7. Save to Chat History
                const string sessionId = "web-search-session";
                _chatHistoryService.AddUserMessage(sessionId, ragRequest.Query);
                _chatHistoryService.AddAssistantMessage(sessionId, botResponse);

                return new RagAnswerResponse { Answer = botResponse };
            }
            catch (Exception ex)
            {
                return new RagAnswerResponse
                {
                    Answer = $"Web Search Error: {ex.Message}"
                };
            }
        }
    }
}