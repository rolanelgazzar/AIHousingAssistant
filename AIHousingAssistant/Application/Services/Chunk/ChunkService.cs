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
using AIHousingAssistant.Application.Services.Embedding;

namespace AIHousingAssistant.Application.Services.Chunk
{
    public class ChunkService : IChunkService
    {
        private readonly ProviderSettings _providerSettings;
        private readonly string _uploadFolder;
        private readonly IEmbeddingService _embeddingService; // Used for SemanticBlocksGrouper

        private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

        public ChunkService(IOptions<ProviderSettings> providerSettings, IEmbeddingService embeddingService)
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

            _embeddingService = embeddingService;
        }

        // -----------------------------------------------------------
        // Main entry: choose chunking mode
        public async Task<List<TextChunk>> CreateChunksAsync(
            string text,
            RagUiRequest ragUiRequest,
            string source)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new List<TextChunk>();

            // Safe source
            source ??= string.Empty;

            // Use switch expression to select the appropriate chunking strategy
            List<TextChunk> chunks = ragUiRequest.ChunkingMode switch
            {
                ChunkingMode.LangChainRecursiveTextSplitter =>
                    await LangChainRecursiveTextSplitter(text, source),

                // Semantic Splitter (using local DI for EmbeddingService)
                ChunkingMode.SemanticTextBlocksGrouper =>
                    await SemanticBlocksGrouperChunks(text, source, ragUiRequest.EmbeddingModel), 

                ChunkingMode.RecursiveTextSplitter =>
                    await RecursiveTextSplitterChunks(text, source),

                // fallback to the widely used LangChain Recursive Splitter
                _ =>
                    await LangChainRecursiveTextSplitter(text, source)
            };

            // Save for transparency/debug
            string chunkingName = System.Enum.GetName(typeof(ChunkingMode), ragUiRequest.ChunkingMode);
            var fileName = $"{_providerSettings.ChunksFileName}-{chunkingName}.json";
            await FileHelper.WriteJsonAsync(_uploadFolder, fileName, chunks);
            await FileHelper.WriteJsonAsync(_uploadFolder, _providerSettings.ChunksFileName, chunks);

            return chunks;
        }

        // -----------------------------------------------------------
        // 1) LangChain.NET RecursiveCharacterTextSplitter (Traditional, fast)
        private Task<List<TextChunk>> LangChainRecursiveTextSplitter(string text, string source)
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
        // 2) SemanticTextBlocksGrouper (Semantic Grouping using local IEmbeddingService)
        // This is the optimized function that adds the Vector to the TextChunk.
        private async Task<List<TextChunk>> SemanticBlocksGrouperChunks(string text, string source,EmbeddingModel embeddingModel)
        {
            // 1. Initial Split: Split text into elementary blocks (sentences in this case)
            var blocks = SemanticTextBlocksGrouper.SplitTextIntoSentences(text);

            if (blocks.Count <= 1)
                return blocks.Select((b, i) => new TextChunk { Index = i, Content = b.Trim(), Source = source, Vector = null }).ToList();

            // 2. Embedding Generation: Generate embeddings for similarity grouping
            var embeddings = new Dictionary<string, float[]>();
            var blockList = blocks.Distinct().ToList();

            foreach (var b in blockList)
                embeddings[b] = await _embeddingService.EmbedAsync(b, embeddingModel); // <--- Embedding generated here

            float threshold = 0.70f;

            // 3. Semantic Grouping: Group blocks that are semantically similar
            // grouped is List<List<string>>
            var grouped = SemanticTextBlocksGrouper.GroupTextBlocksBySimilarity(embeddings, threshold);

            var finalChunksWithVectors = new List<TextChunk>();
            int chunkIndex = 0;

            // 4. Final Split and Packaging: Loop through groups, split them, and assign the vector
            foreach (var group in grouped)
            {
                // The representative vector is the embedding of the first block in the group (semantic cohesion)
                var representativeVector = embeddings[group.First()];

                // Aggregate the group blocks into one large string
                var aggregatedText = group.Aggregate((a, b) => a + " " + b);

                // Recursively split the aggregated text to fit max chunk size (1000)
                var splitBlocks = aggregatedText.RecursiveSplit(1000, 100);

                foreach (var chunkContent in splitBlocks)
                {
                    if (string.IsNullOrWhiteSpace(chunkContent)) continue;

                    finalChunksWithVectors.Add(new TextChunk
                    {
                        Index = chunkIndex++,
                        Content = chunkContent.Trim(),
                        Source = source,
                        Vector = representativeVector
                    });
                }
            }

            return finalChunksWithVectors
                .Where(x => !string.IsNullOrWhiteSpace(x.Content))
                .ToList();
        }
        // -----------------------------------------------------------
        // 3) RecursiveTextSplitterChunks (Custom Recursive, based on paragraphs/separators)
        private Task<List<TextChunk>> RecursiveTextSplitterChunks(string text, string source)
        {
            // RecursiveSplit is an extension method available via 'SemanticTextSplitting'
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