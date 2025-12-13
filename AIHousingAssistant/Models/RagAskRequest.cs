using AIHousingAssistant.Application.Enum;

namespace AIHousingAssistant.Models
{
    public class RagAskRequest
    {
        public string Query { get; set; } = string.Empty;

        public VectorStoreProvider VectorStoreProvider { get; set; } = VectorStoreProvider.InMemory;

        public ChunkingMode ChunkingMode { get; set; } = ChunkingMode.LangChainRecursiveTextSplitter;

        public EmbeddingModel EmbeddingModel { get; set; } = EmbeddingModel.NomicEmbedText;
    }
}
