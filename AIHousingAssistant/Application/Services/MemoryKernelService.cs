using AIHousingAssistant.Application.Services.Interfaces.Tools;
using AIHousingAssistant.Models;
using AIHousingAssistant.Models.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI.Ollama;
using Microsoft.KernelMemory.MemoryDb.Qdrant;
using Microsoft.KernelMemory.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AIHousingAssistant.Application.Enum;
using Microsoft.AspNetCore.Http.HttpResults;

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
                Endpoint = "http://localhost:6333",
            };

            //  Define how to split the PDF into small chunks to avoid "context length" errors
            var textPartitioningOptions = new TextPartitioningOptions
            {
                MaxTokensPerParagraph = 300, // Reduced from 1000 to be safer
                OverlappingTokens = 50       // Reduced overlap
            };

            _memory = new KernelMemoryBuilder()
                .WithOllamaTextGeneration(ollamaTextConfig)
                .WithOllamaTextEmbeddingGeneration(ollamaEmbeddingConfig)
                .WithQdrantMemoryDb(qdrantConfig)
                // Local file storage used by KernelMemory pipelines to temporarily store
                //it persists every step of the document processing
                // uploaded documents, extracted text, and intermediate processing artifacts
                .WithSimpleFileStorage(_providerSettings.ProcessingFolder)
                // This line fixes the "input length exceeds context length" error
                .WithCustomTextPartitioningOptions(textPartitioningOptions)
                // Build KernelMemory in "serverless" mode:
                // everything runs in-process (no queues, no background workers)
                .Build<MemoryServerless>();
        }
       public async Task ProcessDocumentByKernelMemoryAsync(List<IFormFile> files, RagUiRequest ragUiRequest)
        {
            if (files == null || !files.Any()) return;
            var collectionName = _providerSettings.CollectionNameBase + ragUiRequest.SessionId;
            foreach (var file in files)
            {
                var fileName = file.FileName.ToLower();
                var extension = Path.GetExtension(fileName);
                var documentId = $"{Guid.NewGuid():N}";

                // 1. Special Handling: URLs & TXT files (requires reading text first)
                if (extension == ".txt" || fileName.Contains("url"))
                {
                    string textInFile;
                    using (var reader = new StreamReader(file.OpenReadStream()))
                    {
                        textInFile = (await reader.ReadToEndAsync()).Trim();
                    }

                    // If it's a URL, use ImportWebPageAsync
                    if (Uri.TryCreate(textInFile, UriKind.Absolute, out var uriResult)
                        && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
                    {
                        await _memory.ImportWebPageAsync(
                            url: textInFile,
                            documentId: $"web_{documentId}",
                            index: collectionName,  //collectionNmae
                            tags: new TagCollection { { "source", "file-url" }, { "sessionId", ragUiRequest.SessionId } }
                        );
                        continue;
                    }

                    // If it's just text, use ImportTextAsync
                    await _memory.ImportTextAsync(
                        text: textInFile,
                        documentId: $"text_{documentId}",
                        index: collectionName,  //collectionNmae
                        tags: new TagCollection { { "source", "file-text" }, { "sessionId", ragUiRequest.SessionId } }
                    );
                    continue;
                }

                // 2. Unified Handling: Documents (PDF, Word, Excel) AND Images
                // Both use ImportDocumentAsync directly from the stream
                using var uploadStream = file.OpenReadStream();

                // Determine source tag based on extension
                string[] imageExtensions = { ".jpg", ".jpeg", ".png", ".bmp" };
                string sourceTag = imageExtensions.Contains(extension) ? "image" : "upload";
                string idPrefix = imageExtensions.Contains(extension) ? "img" : "file";

                await _memory.ImportDocumentAsync(
                    content: uploadStream,
                    fileName: file.FileName,
                    documentId: $"{idPrefix}_{documentId}",
                    index: collectionName,  //collectionNmae
                    tags: new TagCollection {
                { "source", sourceTag },
                { "sessionId", ragUiRequest.SessionId }
                    }
                );
            }
        }
        public async Task<RagAnswerResponse> AskMemoryKernelAsync(RagUiRequest ragRequest)
        {
            if (string.IsNullOrWhiteSpace(ragRequest.Query))
                throw new ArgumentException("Query cannot be empty");

            // 1. Determine the correct collection (index) name using the session ID
            var collectionName = _providerSettings.CollectionNameBase + ragRequest.SessionId;

            // Hybrid Search (Ask) - Returns a generated answer based on the context
            if (ragRequest.SearchMode == SearchMode.Hybrid)
            {
                // We specify the index (collectionName) to search only in this session's data
                var answer = await _memory.AskAsync(ragRequest.Query,
                    index: collectionName,
                    filter: new MemoryFilter().ByTag("sessionId", ragRequest.SessionId));

                return new RagAnswerResponse
                {
                    Answer = answer.Result,
                    Sources = answer.RelevantSources?.Select(s => s.SourceName).ToList() ?? new List<string>()
                };
            }

            // Vector Search - Returns the EXACT snippets found in documents
            if (ragRequest.SearchMode == SearchMode.Vector)
            {
                // SearchAsync retrieves raw chunks from the specific session index
                var searchResult = await _memory.SearchAsync(ragRequest.Query,
                    index: collectionName,
                    filter: new MemoryFilter().ByTag("sessionId", ragRequest.SessionId),
                    limit: 3);

                // Extracting exact text from the found partitions
                var exactSnippets = searchResult.Results
                    .SelectMany(r => r.Partitions.Select(p => $"[Source: {r.SourceName}]: {p.Text}"))
                    .ToList();

                return new RagAnswerResponse
                {
                    Answer = exactSnippets.Any()
                        ? string.Join("\n\n" + new string('-', 20) + "\n\n", exactSnippets)
                        : "No exact matches found in your uploaded documents.",
                    Sources = searchResult.Results.Select(r => r.SourceName).Distinct().ToList()
                };
            }

            return new RagAnswerResponse { Answer = "Invalid Search Mode", Sources = new List<string>() };
        }
    }
    }
