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
using AIHousingAssistant.Application.Services.Interfaces;
using AIHousingAssistant.Application.Services.ChatTools.Interfaces;

namespace AIHousingAssistant.Application.Services.ChatTools
{
    public class MemoryKernelService : IMemoryKernelService
    {
        private readonly IKernelMemory _memory;
        private readonly ProviderSettings _providerSettings;

        private readonly IChatHistoryService _chatHistoryService;

        public MemoryKernelService(IOptions<ProviderSettings> providerSettings, IChatHistoryService chatHistoryService)
        {
            _providerSettings = providerSettings.Value;
            _chatHistoryService = chatHistoryService;

            //var ollamaTextConfig = new OllamaConfig
            //{
            //    Endpoint = _providerSettings.Ollama.Endpoint,
            //    TextModel = new OllamaModelConfig { ModelName = _providerSettings.Ollama.TextModel }
            //};
            // Using OpenAIConfig because Groq is OpenAI-compatible
            var chatConfig = new OpenAIConfig
            {
                APIKey = _providerSettings.ChatModel.ApiKey, // Replace with your actual API key
                Endpoint = _providerSettings.ChatModel.Endpoint,
                TextModel = _providerSettings.ChatModel.Model // Best for Arabic, English, and complex tables
            };
            var ollamaEmbeddingConfig = new OllamaConfig
            {
                Endpoint = _providerSettings.EmbeddingModel.Endpoint,
                EmbeddingModel = new OllamaModelConfig { ModelName = _providerSettings.EmbeddingModel.DefaultModel }
            };

            var qdrantConfig = new QdrantConfig
            {
                Endpoint = "http://localhost:6333",
            };

            //  Define how to split the PDF into small chunks to avoid "context length" errors
            var textPartitioningOptions = new TextPartitioningOptions
            {
                MaxTokensPerParagraph = 500,
                OverlappingTokens = 100
                // MaxTokensPerParagraph = 300, // for ollama model Reduced from 1000 to be safer
                //  OverlappingTokens = 50       // Reduced overlap
            };
            // Configure the search client to support Arabic by default
            var searchClientConfig = new SearchClientConfig
            {
                // Text returned when no information is found in documents

                // Set temperature to 0 for consistent and factual answers
                Temperature = 0,

                // Number of document chunks to retrieve (100 is high, usually 5-10 is enough for RAG)
                MaxMatchesCount = 10,

                // Ensure enough tokens are reserved for the Arabic response
                AnswerTokens = 1000
            };
            _memory = new KernelMemoryBuilder()
                //.WithOllamaTextGeneration(ollamaTextConfig)
                .WithOpenAITextGeneration(chatConfig) // Replaces WithOllamaTextGeneration
                .WithOllamaTextEmbeddingGeneration(ollamaEmbeddingConfig)
                .WithQdrantMemoryDb(qdrantConfig)
                // Local file storage used by KernelMemory pipelines to temporarily store
                //it persists every step of the document processing
                // uploaded documents, extracted text, and intermediate processing artifacts
                .WithSimpleFileStorage(_providerSettings.ProcessingFolder)
                // This line fixes the "input length exceeds context length" error
                .WithCustomTextPartitioningOptions(textPartitioningOptions)
                .WithSearchClientConfig(searchClientConfig)
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

            var collectionName = _providerSettings.CollectionNameBase + ragRequest.SessionId;

            // English comment: Update chat history with the user's latest message
            _chatHistoryService.AddUserMessage(ragRequest.SessionId, ragRequest.Query);

            var response = new RagAnswerResponse();

            // 1. Hybrid Search Mode (RAG)
            if (ragRequest.SearchMode == SearchMode.Hybrid)
            {
                var enrichedQuery = GetEnrichedQuery(ragRequest);

                var answer = await _memory.AskAsync(enrichedQuery,
                    index: collectionName,
                    filter: new MemoryFilter().ByTag("sessionId", ragRequest.SessionId));

                response.Answer = FormatFinalResponse(answer.Result);
                response.Sources = answer.RelevantSources?.Select(s => s.SourceName).Distinct().ToList() ?? new List<string?>();

                // English comment: Store the AI's natural language response in history
                _chatHistoryService.AddAssistantMessage(ragRequest.SessionId, answer.Result);
            }
            // 2. Vector Search Mode
            else if (ragRequest.SearchMode == SearchMode.Vector)
            {
                var searchResult = await _memory.SearchAsync(ragRequest.Query,
                    index: collectionName,
                    filter: new MemoryFilter().ByTag("sessionId", ragRequest.SessionId),
                    limit: 3);

                var textBlocks = new List<string>();

                foreach (var result in searchResult.Results)
                {
                    // English comment: Add unique source names
                    if (!response.Sources.Contains(result.SourceName))
                        response.Sources.Add(result.SourceName);

                    foreach (var partition in result.Partitions)
                    {
                        textBlocks.Add(partition.Text);

                        // English comment: Capture the real similarity score (relevance) from Kernel Memory
                        response.Similarity.Add((float)partition.Relevance);
                    }
                }

                response.Answer = FormatFinalResponse(string.Join("\n\n", textBlocks));
                _chatHistoryService.AddAssistantMessage(ragRequest.SessionId, "[Vector Search Results Provided]");
            }

            return response;
        }

        private string GetEnrichedQuery(RagUiRequest ragRequest)
        {
            // Retrieve chat history to maintain context during the conversation
            var history = _chatHistoryService.GetChatHistory(ragRequest.SessionId);

            // Take the last 5 messages to provide enough context without overwhelming the model's memory
            var historySummary = history != null
                ? string.Join("\n", history.TakeLast(5).Select(m => $"{m.Role}: {m.Content}"))
                : string.Empty;

            // English comments as requested:
            // This enriched prompt guides the LLM to:
            // 1. Match the user's language (Arabic/English).
            // 2. Reconstruct tables from text chunks.
            // 3. Maintain conversational continuity using history.
            return $@"
        ### System Instructions:
        1. **Language Detection**: Analyze the 'User Question'. If it is in Arabic, you MUST respond in professional Arabic. If it is in English, respond in English.
        2. **Conversational Context**: Use the 'Chat History' to recognize previous topics or user preferences mentioned in this session.
        3. **Data & Table Extraction**: Answer using the provided document context. If you find data that represents a table or a structured list (even if the formatting is broken in the text chunks), reconstruct it into a clean, easy-to-read Markdown table.
        4. **Source Reliability**: Only answer based on the documents. If the answer is not in the context, politely state that you don't have this information.

        ### Chat History:
        {historySummary}

        ### User Question: 
        {ragRequest.Query}

        ### Final Response (formatted as Markdown):";
        }

        private string FormatFinalResponse(string content)
        {
            // English comment: Ensure the answer is clean and trimmed for the UI
            if (string.IsNullOrWhiteSpace(content)) return "No results found.";
            return content.Trim();
        }
    }
}
