using AIHousingAssistant.Application.Services.RagPipeline.Abstractions;
using AIHousingAssistant.Application.Services.RagPipeline.Handlers.Indexing;
using AIHousingAssistant.Application.Services.RagPipeline.Handlers; // Contains Extraction & Refinement
using AIHousingAssistant.Application.Services.RagPipeline.Models;
using AIHousingAssistant.Application.Services.Chunk;
using AIHousingAssistant.Application.Services.VectorStores;
using AIHousingAssistant.Helper;
using AIHousingAssistant.Models;
using AIHousingAssistant.Models.Settings;
using Microsoft.Extensions.Options;

namespace AIHousingAssistant.Application.Services.RagPipeline
{
    public class RagPipelineProcessor : IRagPipelineProcessor
    {
        private readonly IOptions<Settings> _settings;
        private readonly IChunkService _chunkService;
        private readonly IVectorStore _vectorStore;

        // Constructor to inject the necessary services
        public RagPipelineProcessor(
            IOptions<Settings> settings,
            IChunkService chunkService,
            IVectorStore vectorStore)
        {
            _settings = settings;
            _chunkService = chunkService;
            _vectorStore = vectorStore;
        }

        public async Task<RagPipelineRequest> ExecutePipelineAsync(IFormFile file, RagUiRequest uiRequest)
        {
            // 1. Save temp file and prepare the unified RAG request
            var tempPath = await FileHelper.SaveFileAsync(file, _settings.Value.ProcessingFolder);

            var request = new RagPipelineRequest
            {
                FilePath = tempPath,
                Settings = _settings.Value,
                RagUiRequest = uiRequest
            };

            // 2. Initialize all handlers in the chain
            // Note: Fixed naming to MarkItDownHandler to match your file
            var extraction = new MarkDownHandler();
            var metadata = new FileMetaDataHandler();
            var normalization = new TextNormalizationHandler();

            // Indexing Handlers (Injecting services)
            var chunking = new ChunkingHandler(_chunkService);
            var vectorStorage = new VectorStoreHandler(_vectorStore);

            // This handler now saves both request.Content and request.Chunks as JSON
            var fileStorage = new FileStorageHandler();

            // 3. Chain and Execute the workflow
            // Sequence: Extract -> Meta -> Clean -> Chunk -> Embed & Store Vector -> Save Files
            extraction
                .SetNext(metadata)
                .SetNext(normalization)
                .SetNext(chunking)
                .SetNext(vectorStorage)
                .SetNext(fileStorage);

            // Start the execution from the first handler
            var result = await extraction.HandleAsync(request);

            return result;
        }
    }
}
