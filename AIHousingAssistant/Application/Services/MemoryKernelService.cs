using AIHousingAssistant.Application.Services.Interfaces.Tools;
using AIHousingAssistant.Helper;
using AIHousingAssistant.Models;
using AIHousingAssistant.Models.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI.Ollama;
using Microsoft.KernelMemory.MemoryDb.Qdrant;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AIHousingAssistant.Application.Services
{
    public class MemoryKernelService : IMemoryKernelService
    {
        private readonly IKernelMemory _memory;
        private readonly ProviderSettings _providerSettings;

        public MemoryKernelService(IOptions<ProviderSettings> providerSettings)
        {
            _providerSettings = providerSettings.Value;

            var ollamaTextConfig = new OllamaConfig
            {
                Endpoint = _providerSettings.Ollama.Endpoint,
                TextModel = new OllamaModelConfig { ModelName = _providerSettings.Ollama.TextModel }
            };

            var ollamaEmbeddingConfig = new OllamaConfig
            {
                Endpoint = _providerSettings.Ollama.Endpoint,
                EmbeddingModel = new OllamaModelConfig { ModelName = _providerSettings.Ollama.EmbeddingModel }
            };

            var qdrantConfig = new QdrantConfig
            {
                Endpoint = "http://localhost:6333"// _providerSettings.QDrant.Endpoint
            };


            _memory = new KernelMemoryBuilder()
     .WithOllamaTextGeneration(ollamaTextConfig)
     .WithOllamaTextEmbeddingGeneration(ollamaEmbeddingConfig)
     .WithQdrantMemoryDb(qdrantConfig)
     .WithSimpleFileStorage(_providerSettings.ProcessingFolder)
     .Build<MemoryServerless>();

        }

        public async Task ProcessDocumentByKernelMemoryAsync(List<IFormFile> files, RagUiRequest ragUiRequest)
        {
            // Import uploaded files
            if (files != null && files.Any())
            {
                foreach (var file in files)
                {
                    using var stream = file.OpenReadStream();

                    await _memory.ImportDocumentAsync(
                        content: stream,
                        fileName: file.FileName,
                        documentId: $"file-{Guid.NewGuid()}-{file.FileName}",
                        tags: new TagCollection { { "source", "upload" }, { "sessionId", ragUiRequest.SessionId } }
                    );
                }
            }

            // Import query as URL or text
            if (!string.IsNullOrWhiteSpace(ragUiRequest.Query))
            {
                if (Uri.TryCreate(ragUiRequest.Query, UriKind.Absolute, out var uriResult)
                    && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
                {
                    await _memory.ImportWebPageAsync(
                        url: ragUiRequest.Query,
                        documentId: $"web-{Guid.NewGuid()}",
                        tags: new TagCollection { { "source", "web" }, { "sessionId", ragUiRequest.SessionId } }
                    );
                }
                else
                {
                    await _memory.ImportTextAsync(
                        text: ragUiRequest.Query,
                        documentId: $"text-{Guid.NewGuid()}",
                        tags: new TagCollection { { "source", "chat-input" }, { "sessionId", ragUiRequest.SessionId } }
                    );
                }
            }
        }

        public async Task<RagAnswerResponse> AskMemoryKernelAsync(RagUiRequest ragRequest)
        {
            if (string.IsNullOrWhiteSpace(ragRequest.Query))
                throw new ArgumentException("Query cannot be empty");

            var answer = await _memory.AskAsync(ragRequest.Query);

            return new RagAnswerResponse
            {
                Answer = answer.Result,
                Sources = answer.RelevantSources?.Select(s => s.SourceName).ToList() ?? new List<string>()
            };
        }
    }
}
