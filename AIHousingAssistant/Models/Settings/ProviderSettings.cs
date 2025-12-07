namespace AIHousingAssistant.Models.Settings
{
    public class ProviderSettings
    {
        public AzureSettings AzureOpenAI { get; set; } = new();
        public OpenRouterSettings OpenRouterAI { get; set; } = new();
        public OpenAISettings OpenAI { get; set; } = new();
        public OllamaSettings Ollama { get; set; } = new();

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
}
