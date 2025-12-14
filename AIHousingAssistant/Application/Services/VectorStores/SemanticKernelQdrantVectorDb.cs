//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Threading.Tasks;
//using AIHousingAssistant.Application.Services.Interfaces;
//using AIHousingAssistant.Models;
//using Microsoft.Extensions.VectorData;
//using Microsoft.SemanticKernel.Connectors.Qdrant;
//using Microsoft.SemanticKernel.Data;

//namespace AIHousingAssistant.Application.Services.VectorDb
//{
//    /// <summary>
//    /// IVectorDB implementation based on Semantic Kernel VectorStore
//    /// with Qdrant as the underlying vector database.
//    /// 
//    /// This class works directly with raw vectors (float[]) and does NOT
//    /// rely on Semantic Kernel "Memory" APIs (which are deprecated).
//    /// </summary>
//    public class SemanticKernelQdrantVectorDb : IVectorDB
//    {
//        private readonly QdrantVectorStore _vectorStore;

//        /// <summary>
//        /// Creates a new Qdrant-backed vector database using Semantic Kernel VectorStore.
//        /// </summary>
//        /// <param name="endpoint">Qdrant endpoint (e.g. http://localhost:6333)</param>
//        public SemanticKernelQdrantVectorDb(string endpoint)
//        {
//            _vectorStore = new QdrantVectorStore(endpoint);
//        }

//        // -------------------------------------------------
//        // Collection management

//        /// <summary>
//        /// Ensures that a collection exists in Qdrant.
//        /// If the collection does not exist, it will be created.
//        /// </summary>
//        public async Task EnsureCollectionAsync(string collectionName, int vectorSize)
//        {
//            var collection = _vectorStore.GetCollection<VectorChunk>(collectionName);

//            if (!await collection.CollectionExistsAsync())
//            {
//                await collection.CreateCollectionAsync(
//                    new VectorStoreRecordDefinition
//                    {
//                        // Name of the property that holds the vector embedding
//                        VectorPropertyName = nameof(VectorChunk.Embedding),

//                        // Dimensionality of the vector
//                        Dimensions = vectorSize
//                    }
//                );
//            }
//        }

//        /// <summary>
//        /// Checks whether a collection already exists.
//        /// </summary>
//        public async Task<bool> IsCollectionExistedAsync(string collectionName)
//        {
//            var collection = _vectorStore.GetCollection<VectorChunk>(collectionName);
//            return await collection.CollectionExistsAsync();
//        }

//        /// <summary>
//        /// Lists all available collections.
//        /// 
//        /// NOTE: Qdrant VectorStore currently does not expose
//        /// a collection listing API via Semantic Kernel.
//        /// </summary>
//        public Task<List<string>> ListAllCollectionsAsync()
//        {
//            throw new NotSupportedException(
//                "Listing collections is not supported by Semantic Kernel VectorStore for Qdrant."
//            );
//        }

//        /// <summary>
//        /// Returns basic metadata about a collection.
//        /// </summary>
//        public async Task<Dictionary<string, object>> GetCollectionInfoAsync(string collectionName)
//        {
//            return new Dictionary<string, object>
//            {
//                { "name", collectionName },
//                { "exists", await IsCollectionExistedAsync(collectionName) }
//            };
//        }

//        /// <summary>
//        /// Deletes a collection from Qdrant.
//        /// </summary>
//        public async Task<bool> DeleteCollectionAsync(string collectionName)
//        {
//            var collection = _vectorStore.GetCollection<VectorChunk>(collectionName);
//            await collection.DeleteCollectionAsync();
//            return true;
//        }

//        // -------------------------------------------------
//        // Data operations

//        /// <summary>
//        /// Inserts or updates vector records in the collection.
//        /// </summary>
//        public async Task UpsertAsync(string collectionName, List<VectorChunk> vectors)
//        {
//            if (vectors == null || vectors.Count == 0)
//                return;

//            var collection = _vectorStore.GetCollection<VectorChunk>(collectionName);
//            await collection.UpsertAsync(vectors);
//        }

//        /// <summary>
//        /// Searches for the closest vector using cosine similarity.
//        /// Returns the best matching VectorChunk.
//        /// </summary>
//        public async Task<VectorChunk?> SearchClosestAsync(string collectionName, float[] queryVector)
//        {
//            if (queryVector == null || queryVector.Length == 0)
//                return null;

//            var collection = _vectorStore.GetCollection<VectorChunk>(collectionName);

//            var results = await collection.VectorizedSearchAsync(
//                queryVector,
//                new VectorSearchOptions
//                {
//                    Top = 1
//                }
//            );

//            return results.Results.FirstOrDefault()?.Record;
//        }

//        /// <summary>
//        /// Retrieves all records from a collection.
//        /// 
//        /// NOTE: Full enumeration is not efficiently supported by
//        /// vector databases and is generally discouraged.
//        /// </summary>
//        public Task<List<VectorChunk>> GetAllAsync(string collectionName)
//        {
//            throw new NotSupportedException(
//                "Retrieving all vectors is not supported by Semantic Kernel VectorStore."
//            );
//        }
//    }
//}
