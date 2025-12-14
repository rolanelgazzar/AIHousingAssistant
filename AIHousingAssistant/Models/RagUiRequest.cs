using AIHousingAssistant.Application.Enum;

namespace AIHousingAssistant.Models
{
    public class RagUiRequest
    {
        public string Query { get; set; } = "";
        public VectorStoreProvider VectorStoreProvider { get; set; }
        public ChunkingMode ChunkingMode { get; set; } 
        public EmbeddingModel EmbeddingModel { get; set; }
        public  SearchMode SearchMode { get; set; }

    }

}
