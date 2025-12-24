using AIHousingAssistant.Application.Enum;
using AIHousingAssistant.Application.SemanticKernel;
using AIHousingAssistant.Application.Services.Interfaces;
using AIHousingAssistant.Application.Services.Interfaces.Tools;
using AIHousingAssistant.Models;
using AIHousingAssistant.Models.Settings;
using AIHousingAssistant.semantic.Plugins;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace AIHousingAssistant.Application.Services
{
    public class PluginDbService : IPluginDbService
    {
        private readonly IHousingService _housingService;
        private readonly IChatHistoryService _chatHistoryService;
        private readonly ProviderSettings _providerSettings; 
        
        public PluginDbService(
            IHousingService housingService,
            IChatHistoryService chatHistoryService,
             IOptions<ProviderSettings> providerSettings)
        {
            _housingService = housingService;
            _chatHistoryService = chatHistoryService;
            _providerSettings = providerSettings.Value;
        }

        public async Task<RagAnswerResponse> AskPluginDBAsync(RagUiRequest ragRequest)
        {
            try
            {
                // 1. Build the Kernel dynamically based on the UI selected AI Provider
                var kernelBuilder = SemanticKernelHelper.BuildKernel(ragRequest.AIProvider, _providerSettings);

                // 2. Register the Housing Plugin
                SemanticKernelHelper.AddPlugin(kernelBuilder, new HousingPlugin(_housingService));

                var kernel = SemanticKernelHelper.Build(kernelBuilder);

                // 3. Handle Session and Chat History
                // Note: You might need to pass a SessionId from the UI or use a default one
                string sessionId = "default-plugin-session";
                _chatHistoryService.AddUserMessage(sessionId, ragRequest.Query);

                // 4. Configure Tool Call Behavior (Auto-invocation)
                var settings = new OpenAIPromptExecutionSettings
                {
                    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
                };

                // 5. Invoke the Kernel with the user query
                var result = await kernel.InvokePromptAsync(ragRequest.Query, new KernelArguments(settings));
                string botResponse = result.GetValue<string>() ?? "I couldn't find an answer in the database.";

                // 6. Save bot's response to history
                _chatHistoryService.AddAssistantMessage(sessionId, botResponse);

                return new RagAnswerResponse
                {
                    Answer = botResponse,
                 //   Success = true
                };
            }
            catch (Exception ex)
            {
                return new RagAnswerResponse
                {
                    Answer = $"Error in PluginDB Service: {ex.Message}",
                  //  Success = false
                };
            }
        }
    }
}