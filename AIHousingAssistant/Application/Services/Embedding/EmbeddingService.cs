using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AIHousingAssistant.Application.Enum;
using AIHousingAssistant.Application.Services.Interfaces;
using AIHousingAssistant.Helper;
using AIHousingAssistant.Models;
using AIHousingAssistant.Models.Settings;
using Microsoft.Extensions.Options;
using OllamaSharp;

namespace AIHousingAssistant.Application.Services.Embedding
{
    /// <summary>
    /// Implements IEmbeddingService using the OllamaSharp library.
    /// Handles single and batch embedding generation efficiently by utilizing Ollama's capabilities.
    /// </summary>
    public class EmbeddingService : IEmbeddingService
    {
        private readonly OllamaApiClient _client;
        private readonly ProviderSettings _providerSettings;

        // Constructor: Uses Dependency Injection to initialize settings and the Ollama client.
        public EmbeddingService(IOptions<ProviderSettings> providerSettings)
        {
            _providerSettings = providerSettings.Value;
            // The client is initialized with the endpoint from settings
            _client = new OllamaApiClient(_providerSettings.OllamaEmbedding.Endpoint);
            // The initial model is set, but will be dynamically overwritten in the methods
            _client.SelectedModel = _providerSettings.OllamaEmbedding.Model;
        }

        // ------------------------- Single Embedding (Used for Search Query) -------------------------

        /// <summary>
        /// Generates an embedding for a single text string.
        /// </summary>
        public async Task<float[]?> EmbedAsync(string text, EmbeddingModel embeddingModel)
        {
            if (string.IsNullOrWhiteSpace(text))
                return Array.Empty<float>();

            try
            {
                // Normalize the text before embedding
                text = text.Trim().ToLowerInvariant();

                var modelId = embeddingModel.MapToModelId(_providerSettings.OllamaEmbedding);

                // Set the correct model for the client before the call
                _client.SelectedModel = modelId;

                var response = await _client.EmbedAsync(text);

                if (response?.Embeddings != null && response.Embeddings.Count > 0)
                {
                    var embedding = response.Embeddings[0];
                    return embedding.Length > 0 ? embedding : Array.Empty<float>();
                }

                // If the response is valid but contains no embeddings, treat it as a service error
                throw new InvalidOperationException($"Ollama returned an empty embedding list for model {modelId}.");
            }
            catch (Exception ex)
            {
                // Catch any exception (network, API, etc.) and wrap it
                throw new ApplicationException($"Failed to generate single embedding for model {embeddingModel} from Ollama.", ex);
            }
        }

        // ------------------------- Batch Embedding (Used for Ingestion/Storage) -------------------------

        /// <summary>
        /// Generates embeddings for multiple text chunks using parallel processing (Task.WhenAll).
        /// </summary>
        public async Task<List<VectorChunk>> GenerateEmbeddingsAsync(List<TextChunk> chunks, EmbeddingModel embeddingModel)
        {
            if (chunks == null || !chunks.Any())
                return new List<VectorChunk>();

            try
            {
                // 1. Create a task list for parallel embedding generation
                var tasks = chunks.Select(async chunk =>
                {
                    // Call the existing single EmbedAsync function for each chunk in parallel
                    // Note: EmbedAsync now handles its own try-catch and exception throwing.
                    var embedding = await EmbedAsync(chunk.Content, embeddingModel);

                    return new VectorChunk
                    {
                        Content = chunk.Content,
                        Source = chunk.Source,
                        Index = chunk.Index,
                        Embedding = embedding,
                        Similarity = 0
                    };
                });

                // 2. Wait for all embedding tasks to complete. Task.WhenAll aggregates exceptions.
                var allChunks = await Task.WhenAll(tasks);

                var vectorChunks = allChunks
                    .Where(vc => vc.Embedding != null && vc.Embedding.Length > 0)
                    .ToList();

                return vectorChunks;
            }
            catch (AggregateException aex) when (aex.InnerExceptions.Any())
            {
                // Handle cases where one or more parallel tasks failed
                // This is crucial when using Task.WhenAll
                throw new ApplicationException($"Failed to generate batch embeddings. At least one chunk failed to process. See inner exceptions.", aex);
            }
            catch (Exception ex)
            {
                // Catch any other unexpected exception (e.g., if Task.WhenAll itself failed)
                throw new ApplicationException($"An unknown error occurred during batch embedding generation.", ex);
            }
        }
    }
}