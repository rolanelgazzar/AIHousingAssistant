using AIHousingAssistant.Application.Services.RagPipeline.Abstractions;
using AIHousingAssistant.Application.Services.RagPipeline.Models;
using AIHousingAssistant.Application.Services.VectorStores;

namespace AIHousingAssistant.Application.Services.RagPipeline.Handlers
{
    // English comment: This handler persists the generated text chunks and their embeddings into the configured Vector Database.
    public class VectorStoreHandler : RagHandlerBase
    {
        private readonly IVectorStore _vectorStore;

        public VectorStoreHandler(IVectorStore vectorStore)
        {
            _vectorStore = vectorStore;
        }

        public override async Task<RagPipelineRequest> HandleAsync(RagPipelineRequest request)
        {
            // 1. Validation: Ensure we have chunks to store
            if (request.Chunks == null || !request.Chunks.Any())
            {
                return await base.HandleAsync(request);
            }

            // 2. Execution: Store chunks as vectors
            // This calls the StoreTextChunksAsVectorsAsync method which handles 
            // the embedding generation (if missing) and storage logic.
            await _vectorStore.StoreTextChunksAsVectorsAsync(request.Chunks, request.RagUiRequest);

            // 3. Continue to the next handler (e.g., FileStorageHandler)
            return await base.HandleAsync(request);
        }
    }
}