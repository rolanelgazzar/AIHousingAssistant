using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AIHousingAssistant.Application.Enum;
using AIHousingAssistant.Application.Services.Embedding;
using AIHousingAssistant.Application.Services.Interfaces;
using AIHousingAssistant.Application.Services.VectorDb;
using AIHousingAssistant.Models;
using AIHousingAssistant.Models.Settings;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using OllamaSharp;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using static Qdrant.Client.Grpc.Qdrant;

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
            _providerSettings=providerSettings.Value;
            _collectionName = _providerSettings.CollectionName;
        }

        // -------------------------------------------------
        // Ingestion path: store chunks as vectors in Qdrant
    
        public async Task StoreTextChunksAsVectorsAsync(List<TextChunk> chunks, EmbeddingModel embeddingModel)
        {
            // 1) Convert TextChunk -> VectorChunk (using _embeddingService)
            // 2) Ensure collection exists (using _vectorDb.EnsureCollectionAsync)
            // 3) Upsert vectors (using _vectorDb.UpsertAsync)
            if (chunks == null || chunks.Count == 0)
                return;

            // 1) Build VectorChunks (embed in parallel)
            var tasks = chunks.Select(async chunk => new VectorChunk
            {
                Index = chunk.Index,
                Content = chunk.Content,
                Source = chunk.Source,
                Embedding = await _embeddingService.EmbedAsync(chunk.Content)
            });

            var vectorChunks = (await Task.WhenAll(tasks))
                .Where(vc => vc.Embedding != null && vc.Embedding.Length > 0)
                .ToList();

            if (vectorChunks.Count == 0)
                return;

            // 2) Ensure collection exists using vector size
            int vectorSize = vectorChunks[0].Embedding.Length;
            await _vectorDb.EnsureCollectionAsync(_collectionName, vectorSize);

            // 3) Upsert to Qdrant via IVectorDB
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
        public async Task<List<VectorChunk>> HybridSearchAsync(string queryText, int topK = 5)
        {
            if (string.IsNullOrWhiteSpace(queryText))
                return new List<VectorChunk>();

            // 1) Keyword extraction
            var keywords = _vectorDb.ExtractKeywords(queryText) ;
            if (keywords.Count == 0)
                return new List<VectorChunk>();

            // 2) Get all chunks (or filtered subset if you optimize later)
            var allChunks = await _vectorDb.GetAllAsync(_collectionName);
            if (allChunks == null || allChunks.Count == 0)
                return new List<VectorChunk>();

            // 3) Keyword filtering
            var keywordFiltered = allChunks
                .Where(c =>
                    !string.IsNullOrWhiteSpace(c.Content) &&
                    keywords.Any(k =>
                        c.Content.Contains(k, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (keywordFiltered.Count == 0)
                return new List<VectorChunk>();

            // 4) Embed query
            var queryVector = await _embeddingService.EmbedAsync(queryText);
            if (queryVector == null || queryVector.Length == 0)
                return new List<VectorChunk>();

            // 5) Rank by vector similarity
            var ranked = keywordFiltered
                .Select(c => new
                {
                    Chunk = c,
                    Score =_vectorDb. CosineSimilarity(queryVector, c.Embedding)
                })
                .OrderByDescending(x => x.Score)
                .Take(topK)
                .Select(x => x.Chunk)
                .ToList();

            return ranked;
        }

        public async Task<List<VectorChunk>> SemanticSearchAsync(string queryText, int top =5)
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
            return await _vectorDb.GetAllAsync(_collectionName);

        }

    }
}