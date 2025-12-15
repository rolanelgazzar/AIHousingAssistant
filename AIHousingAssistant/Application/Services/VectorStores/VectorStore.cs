using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AIHousingAssistant.Application.Enum;
using AIHousingAssistant.Application.Services.Embedding;
using AIHousingAssistant.Application.Services.Interfaces;
using AIHousingAssistant.Helper;
using AIHousingAssistant.Models;
using AIHousingAssistant.Models.Settings;
using DocumentFormat.OpenXml.Drawing.Charts;
using Microsoft.Extensions.Options;
using Qdrant.Client.Grpc;

// NOTE: We assume that the necessary interfaces (IVectorStore, IEmbeddingService) 
// and models (TextChunk, VectorChunk, EmbeddingModel, ProviderSettings) are defined.
// We also assume the existence of SerachHelper.ExtractKeywords method.

namespace AIHousingAssistant.Application.Services.VectorStores
{
    /// <summary>
    /// Unified VectorStore class implementing IVectorStore.
    /// It handles embedding generation and high-level search logic, relying on the unified IVectorDB.SearchAsync method.
    /// </summary>
    public class VectorStore : IVectorStore
    {
        private readonly IEmbeddingService _embeddingService;
        private readonly ProviderSettings _providerSettings;
        private readonly IVectorDB_Resolver _vectorDbResolver; 
        // Assumed constant payload key for filtering
        private const string ContentPayloadField = "content";

        /// <summary>
        /// Constructor: Uses Dependency Injection.
        /// </summary>
        public VectorStore(
            IEmbeddingService embeddingService,
            IOptions<ProviderSettings> providerSettings,
            IVectorDB_Resolver vectorDbResolver)
            
        {
            _embeddingService = embeddingService;
            _providerSettings = providerSettings.Value;
            _vectorDbResolver = vectorDbResolver;

        }




        // ------------------------- IVectorStore Implementation -------------------------
        private IVectorDB GetVectorDb(RagUiRequest ragUiRequest)
        {
            return _vectorDbResolver.Resolve(ragUiRequest.VectorDbProvider);
        }
        /// <summary>
        /// Stores text chunks as vectors in the Vector DB after generating embeddings in parallel.
        /// </summary>

        public async Task StoreTextChunksAsVectorsAsync(List<TextChunk> chunks, RagUiRequest ragUiRequest)
        {
            if (chunks == null || chunks.Count == 0)
                return;
            // This function handles the parallelism/batching internally in the embedding service.
            var vectorChunks = await _embeddingService.GenerateEmbeddingsAsync(chunks, ragUiRequest.EmbeddingModel);

            // Validation
            var validChunks = vectorChunks
                .Where(vc => vc.Embedding != null && vc.Embedding.Length > 0)
                .ToList();

            if (validChunks.Count == 0)
                return;

            // 2. Ensure collection and Upsert using IVectorDB
            int vectorSize = validChunks[0].Embedding!.Length;

            var vectorDbInstance = GetVectorDb(ragUiRequest);

            await vectorDbInstance.EnsureCollectionAsync(GetCollectionName(ragUiRequest), vectorSize);

            await vectorDbInstance.UpsertAsync(GetCollectionName(ragUiRequest), validChunks);
        }
        // ------------------------- Query Path -------------------------

        /// <summary>
        /// Pure Vector Search: returns the single closest vector chunk (top=1).
        /// </summary>
        public async Task<VectorChunk?> VectorSearchAsync(string queryText , RagUiRequest ragUiRequest)
        {
            // 1. Generate the query vector. 

            var queryVector = await _embeddingService.EmbedAsync(queryText, ragUiRequest.EmbeddingModel);

            // Check for empty array returned by EmbedAsync (i.e., empty input text)
            if (queryVector == null || queryVector.Length == 0)
            {
                return null;
            }

            try
            {
                // 2. Perform the search using the unified method in the Vector DB
                var vectorDbInstance = GetVectorDb(ragUiRequest);

                var results = await vectorDbInstance.SearchAsync(
                    collectionName: GetCollectionName (ragUiRequest),
                    queryVector: queryVector,
                    top: 1,
                    filter: null, // Pure vector search (no keyword filtering)
                    withPayload: true
                );

                // 3. Return the single best result
                return results.FirstOrDefault();
            }
            catch (Exception ex)
            {
                // Catch any exception related to the Vector DB search itself (e.g., Qdrant connection failure)
                throw new ApplicationException($"Failed to perform Vector DB search on collection {_collectionName}.", ex);
            }
        }


        /// <summary>
        /// Pure Semantic Search: returns the top 'top' vector chunks using the unified SearchAsync without a filter.
        /// </summary>
        public async Task<List<VectorChunk>> SemanticSearchAsync(string queryText, RagUiRequest ragUiRequest)
        {
            var queryVector = await _embeddingService.EmbedAsync(queryText, ragUiRequest.EmbeddingModel);
            if (queryVector == null || queryVector.Length == 0)
            {
                return null;
            }

            try
            {
                // Call IVectorDB.SearchAsync with filter = null for pure semantic search
                var vectorDbInstance = GetVectorDb(ragUiRequest);
                var results = await vectorDbInstance.SearchAsync(
                collectionName: GetCollectionName(ragUiRequest),
                queryVector: queryVector,
                top: ragUiRequest.TopSimilarity,
                filter: null, // No payload filtering
                withPayload: true
            );

            return results;
            }
            catch (Exception ex)
            {
                // Catch any exception related to the Vector DB search itself (e.g., Qdrant connection failure)
                throw new ApplicationException($"Failed to perform Vector DB search on collection {GetCollectionName(ragUiRequest)}.", ex);
            }
        }

        /// <summary>
        /// Hybrid Search implementation using filtering capability of the Vector DB (Qdrant).
        /// This creates a filter object from keywords and passes it to the unified SearchAsync.
        /// </summary>
        /// <summary>
        /// Hybrid Search implementation using filtering capability of the Vector DB (Qdrant).
        /// This creates a filter object from keywords and passes it to the unified SearchAsync.
        /// </summary>
        public async Task<List<VectorChunk>> HybridSearchAsync(string queryText, RagUiRequest ragUiRequest)
        {
            var queryVector = await _embeddingService.EmbedAsync(queryText, ragUiRequest.EmbeddingModel);

            if (queryVector == null || queryVector.Length == 0)
                return new List<VectorChunk>();

            // 1) Extract keywords (ASSUMING SearchHelper is correct)
            // NOTE: Replace SearchHelper.ExtractKeywords with the actual class/method name if different.
            var keywords = SearchHelper.ExtractKeywords(queryText); // <-- MISSING LINE ADDED HERE

            object? qdrantFilter = null;

            // 2) If keywords are found, construct the necessary Qdrant filter object
            if (keywords != null && keywords.Count > 0)
            {
        
                qdrantFilter = new
                {
                    Should = keywords.Select(k => new
                    {
                        Match = new { Key = ContentPayloadField, Text = k }
                    }).ToList()
                };
            }

            // 3) Call IVectorDB.SearchAsync with the generated filter (or null if no keywords)
            var vectorDbInstance = GetVectorDb(ragUiRequest);
            try
            {
                var results = await vectorDbInstance.SearchAsync(
                    collectionName: GetCollectionName(ragUiRequest),
                    queryVector: queryVector,
                    top: ragUiRequest.TopSimilarity,
                    filter: qdrantFilter, // Pass the filter object for hybrid search
                    withPayload: true
                );

                return results;
            }
            catch (Exception ex)
            {
                // Catch any exception related to the Vector DB search itself (e.g., Qdrant connection failure)
                throw new ApplicationException($"Failed to perform Hybrid Search on collection {GetCollectionName(ragUiRequest)}.", ex);
            }
        }
        private string GetCollectionName(RagUiRequest ragUiRequest) {
            return _providerSettings.CollectionNameBase
             + System.Enum.GetName(typeof(ChunkingMode), ragUiRequest.ChunkingMode);
        }
    }
}