using AIHousingAssistant.Application.Enum;

namespace AIHousingAssistant.Models.Settings
{
    public class ProviderSettings
    {
        public AzureSettings AzureOpenAI { get; set; } = new();
        public OpenRouterSettings OpenRouterAI { get; set; } = new();
        public OpenAISettings OpenAI { get; set; } = new();
        public OllamaSettings Ollama { get; set; } = new();
        public OllamaEmbeddingSettings OllamaEmbedding { get; set; } = new();

        public QDrantSettings QDrant { get; set; } = new();
        public string  ProcessingFolder { get; set; }
        public string VectorStoreFilename { get; set; }
        public string ChunksFileName { get; set; }
        public VectorStoreProvider VectorStoreProvider { get; set; } = VectorStoreProvider.InMemory;
        public string CollectionNameBase { get; set; }
    }

    public class AzureSettings
    {
        public string Endpoint { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
    }

    public class OpenRouterSettings
    {
        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string ApiUrlDirect { get; set; } = string.Empty;
        public string ApiUrlSkills { get; set; } = string.Empty;
    }

    public class OpenAISettings
    {
        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
    }
    public class OllamaSettings
        {
        public string Endpoint { get; set; } 
    public string Model { get; set; } = string.Empty;
        }
    public class OllamaEmbeddingSettings
    {
        public string Endpoint { get; set; }

        public string Model { get; set; } = string.Empty;

        public string Default { get; set; } = string.Empty;

        public Dictionary<string, string> EmbeddingModelMap { get; set; } = new();

    }

    public class QDrantSettings
    {
        public string Endpoint { get; set; }

    }

}
