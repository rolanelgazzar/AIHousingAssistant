using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
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

namespace AIHousingAssistant.Application.Services.VectorStores
{
    public class QDrantVectorStore_Sdk : IQDrantVectorStore
    {
        private readonly IEmbeddingService _embeddingService;
        private readonly IVectorDB _vectorDb;
        private readonly string _collectionName = "housing_vectors";

        public QDrantVectorStore_Sdk(
            IEmbeddingService embeddingService,
            IVectorDB vectorDb)
        {
            _embeddingService = embeddingService;
            _vectorDb = vectorDb;
        }

        // -------------------------------------------------
        // Ingestion path: store chunks as vectors in Qdrant
    
        public async Task StoreTextChunksAsVectorsAsync(List<TextChunk> chunks)
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
        public async Task<VectorChunk?> SearchClosest(string queryText)
        {
            var queryVector = await _embeddingService.EmbedAsync(queryText);
            if (queryVector == null || queryVector.Length == 0)
                return null;

            return await _vectorDb.SearchClosestAsync(_collectionName, queryVector);
        }


        // -------------------------------------------------
        // Debug/UI path: get all stored vectors
        public async Task<List<VectorChunk>> GetAllAsync()
        {
            return await _vectorDb.GetAllAsync(_collectionName);

        }
    }
}