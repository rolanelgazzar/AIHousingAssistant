using AIHousingAssistant.Application.Enum;

namespace AIHousingAssistant.Models.Settings
{
    public class Settings
    {
        public AzureSettings AzureOpenAI { get; set; } = new();
        public OpenRouterSettings OpenRouterAI { get; set; } = new();
        public OpenAISettings OpenAI { get; set; } = new();
        public GroqSettings Groq { get; set; } = new();
        public OllamaSettings Ollama { get; set; } = new();
        // Property name must match "ChatModel" in JSON
        public ChatModelSettings ChatModel { get; set; } = new();

        // Property name must match "EmbeddingModel" in JSON
        public EmbeddingModelSettings EmbeddingModel { get; set; } = new();

        public QDrantSettings QDrant { get; set; } = new();

        public GoogleConnector GoogleConnector { get; set; } = new();
        public AzureDocIntelSettings AzureDocIntel { get; set; } = new();
        public string  ProcessingFolder { get; set; }
        public string VectorStoreFilename { get; set; }
        public string CollectionNameKernelMemory { get; set; }
        public string CollectionNameCustomRag { get; set; }
        public string ChunksFileName { get; set; }
    }

    public class AzureSettings
    {
        public string Endpoint { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
    }
    public class GroqSettings
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
    public class ChatModelSettings
    {
        public string Endpoint { get; set; } 
    public string Model { get; set; } = string.Empty;

        public string ApiKey { get; set; } = string.Empty;

    }
    public class OllamaSettings
    {
        public string Endpoint { get; set; }
        public string Model { get; set; } = string.Empty;


    }
    public class EmbeddingModelSettings
    {
        public string Endpoint { get; set; }

        public string DefaultModel { get; set; } = string.Empty;


        public Dictionary<string, string> EmbeddingModelMap { get; set; } = new();

    }

    public class QDrantSettings
    {
        public string Endpoint { get; set; }

    }

    public class GoogleConnector
    {
        public string ApiKey { get; set; } = string.Empty;
        public string SearchEngineId { get; set; } = string.Empty;
    }
    public class AzureDocIntelSettings
    {
        public string Endpoint { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
    }
}
