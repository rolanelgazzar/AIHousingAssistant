using AIHousingAssistant.Application.Enum;
using AIHousingAssistant.Application.SemanticKernel;
using AIHousingAssistant.Application.Services.Interfaces;
using AIHousingAssistant.Application.Services.Interfaces.Tools;
using AIHousingAssistant.Models;
using AIHousingAssistant.Models.Settings;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace AIHousingAssistant.Application.Services
{
    public class DirectChatService : IDirectChatService
    {
        private readonly IChatHistoryService _chatHistoryService;
        private readonly ProviderSettings _providerSettings;

        public DirectChatService(
            IChatHistoryService chatHistoryService,
            IOptions<ProviderSettings> providerSettings)
        {
            _chatHistoryService = chatHistoryService;
            _providerSettings = providerSettings.Value;
        }

        public async Task<RagAnswerResponse> AskDirectChatAsync(RagUiRequest ragRequest)
        {
            try
            {
                // 1. Build the Kernel (The Core Engine)
                // This builds the AI model (e.g., GPT-4 or Gemini) based on your UI selection
                var kernelBuilder = SemanticKernelHelper.BuildKernel(ragRequest.AIProvider, _providerSettings);

                // You can add any local plugins here if you want the Planner to use them
                // Example: kernelBuilder.Plugins.AddFromObject(new MyLocalPlugin());

                var kernel = SemanticKernelHelper.Build(kernelBuilder);

                // 2. Manage Session History
                string sessionId = "direct-chat-session";
                _chatHistoryService.AddUserMessage(sessionId, ragRequest.Query);

                // 3. Configure the Planner Behavior (Tool Calling)
                // AutoInvokeKernelFunctions is the modern "Planner". 
                // It allows the AI to plan and execute steps if tools are available.
                var settings = new OpenAIPromptExecutionSettings
                {
                    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                    Temperature = 0.7,
                    MaxTokens = 2000
                };

                // 4. Execution
                // The AI will process the query. If it needs a tool, it will search its registered plugins.
                var result = await kernel.InvokePromptAsync(ragRequest.Query, new KernelArguments(settings));

                string botResponse = result.GetValue<string>() ?? "I'm sorry, I couldn't generate a response.";

                // 5. Save Response to History
                _chatHistoryService.AddAssistantMessage(sessionId, botResponse);

                return new RagAnswerResponse
                {
                    Answer = botResponse
                };
            }
            catch (Exception ex)
            {
                return new RagAnswerResponse
                {
                    Answer = $"Direct Chat Error: {ex.Message}"
                };
            }
        }
    }
}