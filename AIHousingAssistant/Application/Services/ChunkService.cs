using AIHousingAssistant.Application.Enum;
using AIHousingAssistant.Application.Services.Interfaces;
using AIHousingAssistant.Helper;
using AIHousingAssistant.Models;
using AIHousingAssistant.Models.Settings;
using LangChain.Splitters.Text;
using Microsoft.Extensions.Options;
using SemanticTextSplitting;
using System.Text.Json;
using System.Linq;

namespace AIHousingAssistant.Application.Services
{
    public class ChunkService : IChunkService
    {
        private readonly ProviderSettings _providerSettings;
        private readonly string _uploadFolder;

        private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

        public ChunkService(IOptions<ProviderSettings> providerSettings)
        {
            if (providerSettings == null)
                throw new ArgumentNullException(nameof(providerSettings));

            _providerSettings = providerSettings.Value;

            _uploadFolder = Path.Combine(
                Directory.GetCurrentDirectory(),
                _providerSettings.ProcessingFolder
            );

            if (!Directory.Exists(_uploadFolder))
                Directory.CreateDirectory(_uploadFolder);
        }

        // -----------------------------------------------------------
        // Main entry: choose chunking mode
        public async Task<List<TextChunk>> CreateChunksAsync(
            string text,
            ChunkingMode chunkingMode,
            string source)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new List<TextChunk>();

            // Safe source
            source ??= string.Empty;

            List<TextChunk> chunks = chunkingMode switch
            {
                ChunkingMode.LangChainRecursiveTextSplitter =>
                    await LangChainRecursiveTextSplitter(text, source),

                ChunkingMode.SemanticTextSplitter =>
                    await SemanticTextSplitterChunks(text, source),

                ChunkingMode.SemanticTextBlocksGrouper =>
                    await SemanticBlocksGrouperChunks(text, source),

                ChunkingMode.RecursiveTextSplitter =>
                     await RecursiveTextSplitterChunks(text, source),

                // fallback
                _ =>
                    await LangChainRecursiveTextSplitter(text, source)
            };

            // Save for transparency/debug
            var fileName= $"chunks-" + (chunkingMode).ToString() + ".json";
            await FileHelper.WriteJsonAsync(_uploadFolder, _providerSettings.ChunksFileName, chunks);

            return chunks;
        }

        // -----------------------------------------------------------
        // 1) LangChain.NET RecursiveCharacterTextSplitter
        public Task<List<TextChunk>> LangChainRecursiveTextSplitter(string text, string source)
        {
            if (string.IsNullOrWhiteSpace(text))
                return Task.FromResult(new List<TextChunk>());

            var splitter = new RecursiveCharacterTextSplitter(
                chunkSize: 1000,
                chunkOverlap: 100
            );

            var rawChunks = splitter.SplitText(text);

            var chunks = rawChunks
                .Select((c, i) => new TextChunk
                {
                    Index = i,
                    Content = c.Trim(),
                    Source = source
                })
                .Where(tc => !string.IsNullOrWhiteSpace(tc.Content))
                .ToList();

            return Task.FromResult(chunks);
        }

        // -----------------------------------------------------------
        // 2) SemanticTextSplitter (Semantic Kernel + Ollama embeddings)
        // Uses your uploaded: SemanticTextSplitting/SemanticTextSplitter.cs
#pragma warning disable SKEXP0070

        private async Task<List<TextChunk>> SemanticTextSplitterChunks(string text, string source)
        {
            // You can tune these
            int chunkSize = 1000;
            int chunkOverlap = 100;
            float threshold = 0.75f;

            // Paragraph is usually better for documents, Sentence for short/structured text
            var groupingStrategy = GroupingStrategy.Paragraph;

            // Required settings
            var modelId = _providerSettings.Ollama.Model;
            var endpoint = new Uri(_providerSettings.Ollama.Endpoint);

            var rawChunks = await text.SemanticSplitAsync(
                chunkSize: chunkSize,
                chunkOverlap: chunkOverlap,
                modelId: modelId,
                endpoint: endpoint,
                threshold: threshold,
                groupingStrategy: groupingStrategy
            );

            return rawChunks
                .Select((c, i) => new TextChunk
                {
                    Index = i,
                    Content = (c ?? string.Empty).Trim(),
                    Source = source
                })
                .Where(tc => !string.IsNullOrWhiteSpace(tc.Content))
                .ToList();
        }

#pragma warning restore SKEXP0070

        // -----------------------------------------------------------
        // 3) SemanticTextBlocksGrouper (simple “semantic-ish” mode for testing)
        // NOTE: the full power of this class is with embeddings + similarity grouping.
        // Here we start simple: paragraphs/sentences -> TextChunk.
        private Task<List<TextChunk>> SemanticBlocksGrouperChunks(string text, string source)
        {
            // Start with paragraphs. You can switch to sentences if you want.
            var blocks = SemanticTextBlocksGrouper.SplitTextIntoParagraphs(text);

            var chunks = blocks
                .Select((b, i) => new TextChunk
                {
                    Index = i,
                    Content = (b ?? string.Empty).Trim(),
                    Source = source
                })
                .Where(tc => !string.IsNullOrWhiteSpace(tc.Content))
                .ToList();

            return Task.FromResult(chunks);
        }

        private Task<List<TextChunk>> RecursiveTextSplitterChunks(string text, string source)
        {
            // Start with paragraphs. You can switch to sentences if you want.
            // RecursiveSplit is an extension method on string in your SemanticTextSplitting project :contentReference[oaicite:1]{index=1}
            var blocks = text.RecursiveSplit(1000, 100);

            var chunks = blocks
                .Select((b, i) => new TextChunk
                {
                    Index = i,
                    Content = (b ?? string.Empty).Trim(),
                    Source = source
                })
                .Where(tc => !string.IsNullOrWhiteSpace(tc.Content))
                .ToList();

            return Task.FromResult(chunks);
        }
    }
}

public interface IChunkService
{
    Task<List<TextChunk>> CreateChunksAsync(string text, ChunkingMode chunkingMode, string source);
}
