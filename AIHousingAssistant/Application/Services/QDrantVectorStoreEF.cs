using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AIHousingAssistant.Application.Services.Interfaces;
using AIHousingAssistant.Models;
using AIHousingAssistant.Models.Settings;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using OllamaSharp;
using Qdrant.Client;

namespace AIHousingAssistant.Application.Services
{
    /// <summary>
    /// Qdrant-backed vector store using the official Semantic Kernel Qdrant connector.
    /// Embeddings are generated using Ollama and stored/search in Qdrant.
    /// </summary>
    public class QDrantVectorStoreEF : IQDrantVectorStoreEF
    {
        private readonly ProviderSettings _providerSettings;
        private readonly OllamaApiClient _ollamaEmbeddingClient;
        private readonly QdrantClient _clientQdrant;

        // Default Qdrant collection name used by this store
        private const string CollectionName = "housing_vectors";

        public QDrantVectorStoreEF(IOptions<ProviderSettings> providerSettings)
        {
            if (providerSettings is null)
                throw new ArgumentNullException(nameof(providerSettings));

            _providerSettings = providerSettings.Value;

            // Initialize Qdrant client using the configured endpoint
            _clientQdrant = new QdrantClient(_providerSettings.QDrant.Endpoint);

            // Initialize Ollama client used for embeddings
            _ollamaEmbeddingClient = new OllamaApiClient(_providerSettings.OllamaEmbedding.Endpoint);
            _ollamaEmbeddingClient.SelectedModel = _providerSettings.OllamaEmbedding.Model;
        }

        /// <summary>
        /// Convert text to embedding vector using Ollama embeddings model.
        /// </summary>
        public async Task<float[]> TextToVectorAsync(string text)
        {
            // Return an empty vector if the input text is null or whitespace
            if (string.IsNullOrWhiteSpace(text))
                return Array.Empty<float>();

            // Trim the text to remove leading and trailing white spaces
            text = text.Trim();

            // Call Ollama embedding endpoint via OllamaSharp client
            var response = await _ollamaEmbeddingClient.EmbedAsync(text);

            // If the response contains at least one embedding, return the first one
            if (response?.Embeddings != null && response.Embeddings.Count > 0)
            {
                var embedding = response.Embeddings[0];

                if (embedding != null && embedding.Length > 0)
                    return embedding;
            }

            // Fallback: return an empty vector if no valid embedding was produced
            return Array.Empty<float>();
        }

        /// <summary>
        /// Store all text chunks as vectors in Qdrant using the Semantic Kernel Qdrant connector.
        /// </summary>
        public async Task StoreTextChunksAsVectorsAsync(List<TextChunk> chunks)
        {
            // Return early if there are no chunks to store
            if (chunks == null || chunks.Count == 0)
                return;

            // Create a Qdrant vector store wrapper around the existing client
            var vectorStore = new QdrantVectorStore(_clientQdrant, ownsClient: false);

            // Get a typed collection mapped to RagChunkRecord using string keys
            var collection = vectorStore.GetCollection<string, RagChunkRecord>(CollectionName);

            // Ensure the Qdrant collection exists based on the RagChunkRecord schema
            await collection.EnsureCollectionExistsAsync();

            // Prepare a batch of records to upsert
            var records = new List<RagChunkRecord>();

            foreach (var chunk in chunks)
            {
                // Skip chunks without any content
                if (string.IsNullOrWhiteSpace(chunk.Content))
                    continue;

                // Generate an embedding for the current chunk content
                var embedding = await TextToVectorAsync(chunk.Content);

                // Skip this chunk if no valid embedding was produced
                if (embedding == null || embedding.Length == 0)
                    continue;

                // Build a record to be stored in Qdrant
                var record = new RagChunkRecord
                {
                    // Use the chunk index as a stable string key
                    Id = chunk.Index.ToString(),

                    // Store the original text content for later retrieval
                    Content = chunk.Content,

                    // Optional source information (file name, section, etc.)
                    Source = chunk.Source ?? string.Empty,

                    // Store the embedding vector used for similarity search
                    Embedding = embedding
                };

                records.Add(record);
            }

            // If there are no valid records, there is nothing to upsert
            if (records.Count == 0)
                return;

            // Upsert all records into Qdrant in a single batch operation
            await collection.UpsertAsync(records);
        }

        /// <summary>
        /// Search Qdrant for the closest chunk to the given query text and map it back to VectorChunk.
        /// </summary>
        public async Task<VectorChunk?> SearchClosest(string queryText)
        {
            // Return null if the query text is empty or whitespace
            if (string.IsNullOrWhiteSpace(queryText))
                return null;

            // Convert the query text into an embedding using Ollama
            var queryEmbedding = await TextToVectorAsync(queryText);

            // If no valid embedding was produced, there is nothing to search with
            if (queryEmbedding == null || queryEmbedding.Length == 0)
                return null;

            // Create a Qdrant vector store wrapper around the existing client
            var vectorStore = new QdrantVectorStore(_clientQdrant, ownsClient: false);

            // Get a typed collection mapped to RagChunkRecord using string keys
            var collection = vectorStore.GetCollection<string, RagChunkRecord>(CollectionName);

            // Ensure the Qdrant collection exists before searching
            await collection.EnsureCollectionExistsAsync();

            // Perform a vector search in Qdrant for the top-1 most similar record
            var searchResults = collection.SearchAsync(
                queryEmbedding, // embedding vector of the query text
                top: 1          // request only the closest match
            );

            // Manually read the first result from the async stream
            VectorSearchResult<RagChunkRecord>? firstResult = null;

            await foreach (var result in searchResults)
            {
                firstResult = result;
                break; // We only need the first match (top-1)
            }

            // If no record was found, return null
            if (firstResult == null || firstResult.Record == null)
                return null;

            var record = firstResult.Record;

            // Try to parse the original chunk index from the record Id
            int index = 0;
            int.TryParse(record.Id, out index);

            // Map the search result back into your VectorChunk model for the RAG layer
            return new VectorChunk
            {
                Index = index,
                Content = record.Content,
                // Score is double? so we cast it to float and default to 0 if null
                Similarity = (float)(firstResult.Score ?? 0.0),
                // The embedding is not needed at the RAG level, so we return an empty array
                Embedding = Array.Empty<float>()
            };

        }

        /// <summary>
        /// Full collection enumeration is not supported in this implementation.
        /// This method currently returns an empty list by design.
        /// </summary>
        public Task<List<VectorChunk>> GetAllAsync()
        {
            // QdrantVectorStore does not provide a simple "get all records"
            // API without tracking all record keys separately.
            //
            // For now, this method returns an empty list to indicate that
            // full collection enumeration is not supported in this implementation.
            // 
            // If you later decide to maintain a list of all keys (for example in a
            // separate table or file), you can update this method to:
            //  - fetch all keys
            //  - call collection.GetBatchAsync(keys)
            //  - map the results into List<VectorChunk>.
            return Task.FromResult(new List<VectorChunk>());
        }
    }

    /// <summary>
    /// Helper extensions for working with IAsyncEnumerable sequences.
    /// This is not related to Entity Framework; it works directly on IAsyncEnumerable.
    /// </summary>
    internal static class AsyncEnumerableExtensions
    {
        /// <summary>
        /// Materializes an IAsyncEnumerable into a List&lt;T&gt;.
        /// This is useful when you want to buffer all items from an async stream.
        /// </summary>
        public static async Task<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> source)
        {
            var list = new List<T>();

            await foreach (var item in source)
            {
                list.Add(item);
            }

            return list;
        }
    }
}
