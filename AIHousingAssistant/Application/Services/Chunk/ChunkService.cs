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
                    await LangChainRecursiveTextSplitter(text, source, ragUiRequest.EmbeddingModel),

                // Semantic Splitter (using local DI for EmbeddingService)
                ChunkingMode.SemanticTextBlocksGrouper =>
                    await SemanticBlocksGrouperChunks(text, source, ragUiRequest.EmbeddingModel), 

                ChunkingMode.RecursiveTextSplitter =>
                    await RecursiveTextSplitterChunks(text, source, ragUiRequest.EmbeddingModel),

                // fallback to the widely used LangChain Recursive Splitter
                _ =>
                    await LangChainRecursiveTextSplitter(text, source, ragUiRequest.EmbeddingModel)
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
        private Task<List<TextChunk>> LangChainRecursiveTextSplitter(
            string text,
            string source,
            EmbeddingModel embeddingModel) // Added model parameter
        {
            if (string.IsNullOrWhiteSpace(text))
                return Task.FromResult(new List<TextChunk>());

            // English comment: Dynamically calculate chunk size based on the specific embedding model 
            // and the language of the input text to ensure optimal token utilization.
            int safeSize = embeddingModel.GetSafeChunkSize(text);
            int overlap = safeSize / 10;

            // English comment: Initialize LangChain's splitter with dynamic parameters instead of hardcoded 1000/100.
            var splitter = new RecursiveCharacterTextSplitter(
                chunkSize: safeSize,
                chunkOverlap: overlap
            );

            var rawChunks = splitter.SplitText(text);

            var chunks = rawChunks
                .Select((c, i) => new TextChunk
                {
                    Index = i,
                    Content = c.Trim(),
                    Source = source
                    // Note: Embedding is usually generated in the next step of the pipeline.
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
        private async Task<List<TextChunk>> SemanticBlocksGrouperChunks(
      string text,
      string source,
      EmbeddingModel embeddingModel)
        {
            try
            {
                // 1. Initial Sentence Splitting
                // Split the text into sentences using the utility from SemanticTextBlocksGrouper
                var rawSentences = SemanticTextBlocksGrouper.SplitTextIntoSentences(text);

                // 2. Determine model-specific safe chunk size based on the text language
                // Uses token-aware safe size for both English and Arabic
                int safeChunkSize = embeddingModel.GetSafeChunkSize(text);
                int overlap = safeChunkSize / 10; // 10% overlap for semantic context

                // 3. Split long sentences into blocks based on the safe chunk size
                var blocks = new List<string>();
                foreach (var sentence in rawSentences)
                {
                    // Check if sentence exceeds max token limit
                    if (embeddingModel.EstimateTokenCount(sentence) > embeddingModel.GetMaxTokenLimit())
                    {
                        // Recursive split for very long sentences
                        blocks.AddRange(sentence.RecursiveSplit(safeChunkSize, overlap));
                    }
                    else
                    {
                        blocks.Add(sentence);
                    }
                }

                if (blocks.Count == 0) return new List<TextChunk>();

                // --- PERFORMANCE OPTIMIZATION: Concurrency Control ---
                // Use a Semaphore to limit the number of concurrent embedding API calls
                using var semaphore = new SemaphoreSlim(10);
                var embeddings = new ConcurrentDictionary<string, float[]>();
                var blockList = blocks.Where(b => !string.IsNullOrWhiteSpace(b)).Distinct().ToList();

                // 4. Parallel Embedding Generation (for grouping)
                var embeddingTasks = blockList.Select(async b =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        var emb = await _embeddingService.EmbedAsync(b, embeddingModel);
                        if (emb != null) embeddings[b] = emb;
                    }
                    catch (Exception ex)
                    {
                        // Log the error and continue with other blocks
                        Console.WriteLine($"[Embedding Error] Model: {embeddingModel}, Msg: {ex.Message}");
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });
                await Task.WhenAll(embeddingTasks);

                // 5. Semantic Grouping
                float threshold = 0.70f; // Cosine similarity threshold
                var grouped = SemanticTextBlocksGrouper.GroupTextBlocksBySimilarity(
                    embeddings.ToDictionary(kv => kv.Key, kv => kv.Value),
                    threshold);

                var finalChunksWithVectors = new List<TextChunk>();
                int chunkIndex = 0;

                // 6. Final Aggregation and Re-Embedding (Crucial for vector accuracy)
                foreach (var group in grouped)
                {
                    if (group == null || !group.Any()) continue;

                    // Aggregate the group into one semantic block
                    var aggregatedText = string.Join(" ", group);

                    // Split aggregated text into RAG-friendly chunks based on safe chunk size
                    var finalSubBlocks = aggregatedText.RecursiveSplit(
                        embeddingModel.GetSafeChunkSize(aggregatedText),
                        overlap
                    );

                    foreach (var chunkContent in finalSubBlocks)
                    {
                        if (string.IsNullOrWhiteSpace(chunkContent)) continue;

                        // Re-embed the final chunk to accurately represent its semantic meaning
                        var finalEmb = await _embeddingService.EmbedAsync(chunkContent, embeddingModel);

                        finalChunksWithVectors.Add(new TextChunk
                        {
                            Index = chunkIndex++,
                            Content = chunkContent.Trim(),
                            Source = source,
                            Embedding = finalEmb
                        });
                    }
                }

                return finalChunksWithVectors;
            }
            catch (Exception)
            {
                throw;
            }
        }

        private Task<List<TextChunk>> RecursiveTextSplitterChunks(string text, string source, EmbeddingModel embeddingModel)
        {
            // English comment: Determine the safe chunk size dynamically based on the model and language.
            // This ensures that even simple recursive splitting respects model token limits.
            int safeSize = embeddingModel.GetSafeChunkSize(text);
            int overlap = safeSize / 10;

            // English comment: Use the smart safeSize instead of the hardcoded 1000.
            var blocks = text.RecursiveSplit(safeSize, overlap);

            var chunks = blocks
                .Select((b, i) => new TextChunk
                {
                    Index = i,
                    Content = (b ?? string.Empty).Trim(),
                    Source = source
                    // Note: Embedding is usually added later in the pipeline for this simple splitter
                })
                .Where(tc => !string.IsNullOrWhiteSpace(tc.Content))
                .ToList();

            return Task.FromResult(chunks);
        }
        // English comment: This method generates synthetic Q&A from chunks and stores them as vectors.
        // This is more accurate for RAG as it matches user intent directly.
    }
}