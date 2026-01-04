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
using System.Collections.Concurrent;

namespace AIHousingAssistant.Application.Services.Chunk
{
    public class ChunkService : IChunkService
    {
        private readonly Settings _providerSettings;
        private readonly string _uploadFolder;
        private readonly IEmbeddingService _embeddingService; // Used for SemanticBlocksGrouper

        private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

        public ChunkService(IOptions<Settings> providerSettings, IEmbeddingService embeddingService)
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

           // Save for transparency / debug

           //string chunkingMode = System.Enum.GetName(typeof(ChunkingMode), ragUiRequest.ChunkingMode);
           //var fileName = $"{_providerSettings.ChunksFileName}--{FileHelper.GetFileNameWithoutExtension(source)}--{chunkingMode}.json";
           // await FileHelper.WriteJsonAsync(_uploadFolder, fileName, chunks);
           // await FileHelper.WriteJsonAsync(_uploadFolder, _providerSettings.ChunksFileName, chunks);

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
        // English comment: Groups text into chunks based on semantic similarity.
        // Ensures that groups are still split if they exceed the max character limit.
        private async Task<List<TextChunk>> SemanticBlocksGrouperChunks(string text, string source, EmbeddingModel embeddingModel)
        {
            try
            {
                // 1. Initial Sentence Splitting
                var rawBlocks = SemanticTextBlocksGrouper.SplitTextIntoSentences(text);

                // English comment: Pre-split oversized sentences to prevent "Context Length Exceeded" errors in Ollama
                var blocks = new List<string>();
                foreach (var rb in rawBlocks)
                {
                    // English comment: If a sentence is unusually long (e.g., > 2000 chars), split it before embedding
                    if (rb.Length > 2000)
                        blocks.AddRange(rb.RecursiveSplit(2000, 0));
                    else
                        blocks.Add(rb);
                }

                if (blocks.Count <= 1)
                {
                    return blocks.Select((b, i) => new TextChunk
                    { Index = i, Content = b.Trim(), Source = source, Embedding = null }).ToList();
                }

                // 2. Parallel Embedding Generation with thread-safe storage
                var embeddings = new ConcurrentDictionary<string, float[]>();
                var blockList = blocks.Where(b => !string.IsNullOrWhiteSpace(b)).Distinct().ToList();

                var embeddingTasks = blockList.Select(async b =>
                {
                    try
                    {
                        var emb = await _embeddingService.EmbedAsync(b, embeddingModel);
                        if (emb != null) embeddings[b] = emb;
                    }
                    catch (Exception ex)
                    {
                        // English comment: Skip problematic blocks to avoid crashing the whole file process
                        Console.WriteLine($"[Ollama Error] Skipping block due to context/format issues: {ex.Message}");
                        throw;

                    }
                });
                await Task.WhenAll(embeddingTasks);

                // 3. Perform Semantic Grouping based on similarity threshold
                float threshold = 0.70f;
                var finalEmbeddingsDict = embeddings.ToDictionary(kv => kv.Key, kv => kv.Value);
                var grouped = SemanticTextBlocksGrouper.GroupTextBlocksBySimilarity(finalEmbeddingsDict, threshold);

                var finalChunksWithVectors = new List<TextChunk>();
                int chunkIndex = 0;

                // 4. Aggregate groups and apply final size limits
                foreach (var group in grouped)
                {
                    if (group == null || !group.Any()) continue;

                    // Use the group leader's vector
                    if (!embeddings.TryGetValue(group.First(), out var representativeVector)) continue;

                    var aggregatedText = string.Join(" ", group);

                    // English comment: Ensure the final chunk does not exceed 1000 characters for optimal RAG
                    var splitBlocks = aggregatedText.RecursiveSplit(1000, 100);

                    foreach (var chunkContent in splitBlocks)
                    {
                        if (string.IsNullOrWhiteSpace(chunkContent)) continue;

                        finalChunksWithVectors.Add(new TextChunk
                        {
                            Index = chunkIndex++,
                            Content = chunkContent.Trim(),
                            Source = source,
                            Embedding = representativeVector
                        });
                    }
                }

                return finalChunksWithVectors;
            }
            catch (Exception ex)
            {
                // English comment: Re-throw exception as requested
                throw;
            }
        }
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

        // English comment: This method generates synthetic Q&A from chunks and stores them as vectors.
        // This is more accurate for RAG as it matches user intent directly.
    }
}