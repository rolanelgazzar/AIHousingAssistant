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
            // English comment: Switch between different AI providers based on configuration
            var builder = provider switch
            {
                AIProvider.AzureOpenAI => BuildWithAzure(settings.AzureOpenAI),
                AIProvider.OpenRouter => BuildWithOpenRouter(settings.OpenRouterAI),
                AIProvider.Ollama => BuildWithOllama(settings.Ollama),
                // English comment: Direct integration for Groq LPU
                AIProvider.Groq => BuildWithGroq(settings.Groq),
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

        private static IKernelBuilder BuildWithGroq(GroqSettings settings)
        {
            // English comment: Groq is OpenAI-compliant, so we use the OpenAI connector 
            // but point it to the Groq API endpoint.
            var builder = Kernel.CreateBuilder();
            builder.AddOpenAIChatCompletion(
                modelId: settings.Model,
                apiKey: settings.ApiKey,
                endpoint: new Uri(settings.Endpoint) // Standard: https://api.groq.com/openai/v1
            );
            return builder;
        }

        private static IKernelBuilder BuildWithOllama(OllamaSettings ollama)
        {
            var builder = Kernel.CreateBuilder();
            builder.AddOllamaChatCompletion(
                modelId: ollama.Model,
                endpoint: new Uri(ollama.Endpoint)
            );
            return builder;
        }

        private static IKernelBuilder BuildWithSemanticOnly()
        {
            return Kernel.CreateBuilder();
        }

        // ---------------------------
        // Helper Methods
        // ---------------------------

        public static void AddPlugin(IKernelBuilder kernel, object plugin)
        {
            kernel.Plugins.AddFromObject(plugin);
        }

        public static Kernel Build(IKernelBuilder kernel)
        {
            return kernel.Build();
        }

        public static OpenAIPromptExecutionSettings? GetDefaultPromptSettings(AIProvider provider)
        {
            // English comment: Define default execution behavior for different providers
            return provider switch
            {
                AIProvider.AzureOpenAI => new OpenAIPromptExecutionSettings
                {
                    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
                },
                AIProvider.OpenRouter => new OpenAIPromptExecutionSettings
                {
                    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
                },
                // English comment: Groq settings with Temperature 0 for factual RAG accuracy
                AIProvider.Groq => new OpenAIPromptExecutionSettings
                {
                    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                    Temperature = 0,
                    MaxTokens = 1024
                },
                AIProvider.Ollama => null,
                _ => new OpenAIPromptExecutionSettings
                {
                    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
                }
            };
        }
    }
}