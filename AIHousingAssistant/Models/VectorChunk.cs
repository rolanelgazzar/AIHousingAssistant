namespace AIHousingAssistant.Models
{
    public class VectorChunk
    {
        public int Index { get; set; }
        public string Content { get; set; }
        public float[] Embedding { get; set; } // store vector representation
    }
}
