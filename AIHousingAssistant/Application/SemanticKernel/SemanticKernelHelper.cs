using AIHousingAssistant.Application.Enum;
using AIHousingAssistant.Models.Settings;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Ollama;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System;
using System.Net.Http;

namespace AIHousingAssistant.Application.SemanticKernel
{
    public static class SemanticKernelHelper
    {
        /// <summary>
        /// Returns an IKernelBuilder preconfigured with the chosen AI provider.
        /// Plugins can be added afterward using AddPlugin().
        /// </summary>
        public static IKernelBuilder BuildKernel(AIProvider provider, ProviderSettings settings)
        {
            var builder = provider switch
            {
                AIProvider.AzureOpenAI => BuildWithAzure(settings.AzureOpenAI),
                AIProvider.OpenRouter => BuildWithOpenRouter(settings.OpenRouterAI),
                AIProvider.OpenAI => BuildWithOpenAI(settings.OpenAI),
                _ => BuildWithSemanticOnly()
            };

            return builder;
        }

        // ---------------------------
        // Internal helpers for each AI provider
        // ---------------------------
        private static IKernelBuilder BuildWithAzure(AzureSettings azure)
        {
            var builder = Kernel.CreateBuilder();
            builder.AddAzureOpenAIChatCompletion(
                deploymentName: azure.Model,
                endpoint: azure.Endpoint,
                apiKey: azure.ApiKey
            );
            return builder;
        }

        private static IKernelBuilder BuildWithOpenRouter(OpenRouterSettings router)
        {
            var builder = Kernel.CreateBuilder();
            builder.AddOpenAIChatCompletion(
                modelId: router.Model,
                apiKey: router.ApiKey,
                httpClient: new HttpClient { BaseAddress = new Uri(router.ApiUrlSkills) }
            );
            return builder;
        }

        private static IKernelBuilder BuildWithOpenAI(OpenAISettings openAI)
        {
            var builder = Kernel.CreateBuilder();
            builder.AddOpenAIChatCompletion(openAI.Model, openAI.ApiKey);
            return builder;
        }

        private static IKernelBuilder BuildWithSemanticOnly()
        {
            return Kernel.CreateBuilder();
        }
        private static IKernelBuilder BuildWithOllama(OllamaSettings ollama)
        {
            // Create a kernel builder
            var builder = Kernel.CreateBuilder();

            // Add Ollama chat completion connector
            builder.AddOllamaChatCompletion(
                modelId: ollama.Model,           // Model name from settings
                endpoint: new Uri(ollama.Endpoint) // Ollama endpoint
                                                   // serviceId: ollama.ServiceId    // Optional
            );

            return builder;
        }
        // ---------------------------
        // Add plugin to the kernel builder
        // ---------------------------
        public static void AddPlugin(IKernelBuilder kernel, object plugin)
        {
            kernel.Plugins.AddFromObject(plugin);
        }

        // ---------------------------
        // Build the final Kernel
        // ---------------------------
        public static Kernel Build(IKernelBuilder kernel)
        {
            return kernel.Build();
        }
        public static OpenAIPromptExecutionSettings? GetDefaultPromptSettings(AIProvider provider)
        {
            // Return default prompt execution settings for providers that support OpenAI settings
            return provider switch
            {
                AIProvider.AzureOpenAI => new OpenAIPromptExecutionSettings
                {
                    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
                },
                AIProvider.OpenAI => new OpenAIPromptExecutionSettings
                {
                    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
                },
                AIProvider.OpenRouter => new OpenAIPromptExecutionSettings
                {
                    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
                },
                AIProvider.Ollama => null, // Ollama does not use OpenAIPromptExecutionSettings
                _ => new OpenAIPromptExecutionSettings
                {
                    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
                }
            };

        }


    }
}
