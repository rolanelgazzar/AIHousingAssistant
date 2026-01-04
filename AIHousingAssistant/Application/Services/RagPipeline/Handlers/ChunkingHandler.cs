using AIHousingAssistant.Application.Services.RagPipeline.Abstractions;
using AIHousingAssistant.Application.Services.Chunk;
using AIHousingAssistant.Application.Services.RagPipeline.Models;

namespace AIHousingAssistant.Application.Services.RagPipeline.Handlers.Indexing
{
    // English comment: This handler is responsible for splitting the document content into smaller chunks 
    // based on the selected chunking strategy in the RagUiRequest.
    public class ChunkingHandler : RagHandlerBase
    {
        private readonly IChunkService _chunkService;

        public ChunkingHandler(IChunkService chunkService)
        {
            _chunkService = chunkService;
        }

        public override async Task<RagPipelineRequest> HandleAsync(RagPipelineRequest request)
        {
            // 1. Validation: If no content was extracted or cleaned, skip chunking
            if (string.IsNullOrEmpty(request.NormalizedContent))
            {
                return await base.HandleAsync(request);
            }

            // 2. Execute Chunking: Use the ChunkService logic to generate the list of TextChunks
            // We pass the clean content, UI preferences (ChunkingMode), and the original file path as the source.
            request.Chunks = await _chunkService.CreateChunksAsync(
                request.NormalizedContent,
                request.RagUiRequest,
                request.FilePath
            );

            // 3. Move to the next step in the pipeline (e.g., VectorEmbeddingHandler)
            return await base.HandleAsync(request);
        }
    }
}