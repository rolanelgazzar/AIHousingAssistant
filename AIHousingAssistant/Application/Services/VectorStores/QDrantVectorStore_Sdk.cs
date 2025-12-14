using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AIHousingAssistant.Application.Enum;
using AIHousingAssistant.Application.Services.Embedding;
using AIHousingAssistant.Application.Services.Interfaces;
using AIHousingAssistant.Application.Services.VectorDb;
using AIHousingAssistant.Models;
using AIHousingAssistant.Models.Settings;
using Microsoft.Extensions.Options;

namespace AIHousingAssistant.Application.Services.VectorStores
{
    public class QDrantVectorStore_Sdk : IVectorStore
    {
        private readonly IEmbeddingService _embeddingService;
        private readonly IVectorDB _vectorDb;
        private readonly string _collectionName;
        private readonly ProviderSettings _providerSettings;

        public QDrantVectorStore_Sdk(
            IEmbeddingService embeddingService,
            IVectorDB vectorDb,
            IOptions<ProviderSettings> providerSettings)
        {
            _embeddingService = embeddingService;
            _vectorDb = vectorDb;
            _providerSettings = providerSettings.Value;
            _collectionName = _providerSettings.CollectionName;
        }

        // -------------------------------------------------
        // Ingestion path: store chunks as vectors in Qdrant
        public async Task StoreTextChunksAsVectorsAsync(List<TextChunk> chunks, EmbeddingModel embeddingModel)
        {
            if (chunks == null || chunks.Count == 0)
                return;

            var tasks = chunks.Select(async chunk => new VectorChunk
            {
                Index = chunk.Index,
                Content = chunk.Content,
                Source = chunk.Source,
                Embedding = await _embeddingService.EmbedAsync(chunk.Content, embeddingModel)
            });

            var vectorChunks = (await Task.WhenAll(tasks))
                .Where(vc => vc.Embedding != null && vc.Embedding.Length > 0)
                .ToList();

            if (vectorChunks.Count == 0)
                return;

            int vectorSize = vectorChunks[0].Embedding.Length;
            await _vectorDb.EnsureCollectionAsync(_collectionName, vectorSize);

            await _vectorDb.UpsertAsync(_collectionName, vectorChunks);
        }

        // -------------------------------------------------
        // Query path: find closest chunk by cosine similarity (Qdrant search)
        public async Task<VectorChunk?> VectorSearchAsync(string queryText)
        {
            var queryVector = await _embeddingService.EmbedAsync(queryText);
            if (queryVector == null || queryVector.Length == 0)
                return null;

            return await _vectorDb.SearchVectorAsync(_collectionName, queryVector);
        }

        // -------------------------------------------------
        // ✅ UPDATED: Hybrid Search without GetAllAsync (no full scan)
        public async Task<List<VectorChunk>> HybridSearchAsync(string queryText, int topK = 5)
        {
            if (string.IsNullOrWhiteSpace(queryText))
                return new List<VectorChunk>();

            // 1) Extract keywords from query (for filter)
            var keywords = _vectorDb.ExtractKeywords(queryText);

            // 2) Embed query
            var queryVector = await _embeddingService.EmbedAsync(queryText);
            if (queryVector == null || queryVector.Length == 0)
                return new List<VectorChunk>();

            // 3) If no keywords, fallback to pure semantic topK
            //    (Hybrid = semantic + keyword boost; without keywords it becomes semantic)
            if (keywords == null || keywords.Count == 0)
            {
                var semanticOnly = await _vectorDb.SearchTopAsync(_collectionName, queryVector, topK);
                return semanticOnly ?? new List<VectorChunk>();
            }

            // 4) ✅ Do filtered vector search inside Qdrant (NO GetAll)
            //    This method must be implemented in QdrantVectorDb_Sdk.
            var filtered = await _vectorDb.SearchTopFilteredAsync(
                collectionName: _collectionName,
                queryVector: queryVector,
                topK: topK,
                payloadTextField: "content",
                keywords: keywords
            );

            return filtered ?? new List<VectorChunk>();
        }

        // -------------------------------------------------
        public async Task<List<VectorChunk>> SemanticSearchAsync(string queryText, int top = 5)
        {
            if (string.IsNullOrWhiteSpace(queryText))
                return new List<VectorChunk>();

            var queryVector = await _embeddingService.EmbedAsync(queryText);
            if (queryVector == null || queryVector.Length == 0)
                return new List<VectorChunk>();

            var results = await _vectorDb.SearchTopAsync(
                _collectionName,
                queryVector,
                top
            );

            return results ?? new List<VectorChunk>();
        }

        // -------------------------------------------------
        // Debug/UI path: get all stored vectors
        public async Task<List<VectorChunk>> GetAllAsync()
        {
            // keep as-is (for debug UI). Hybrid no longer depends on it.
            return await _vectorDb.GetAllAsync(_collectionName);
        }
    }
}
