namespace AIHousingAssistant.Models
{
    public class RagAnswerResponse
    {
        public string Answer { get; set; } = string.Empty;
        public List<int> ChunkIndexes { get; set; } = new();
        public List<string?> Sources { get; set; } = new();
        public List<float> Similarity { get; set; } = new();
    }

}
