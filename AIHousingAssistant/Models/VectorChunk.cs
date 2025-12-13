namespace AIHousingAssistant.Models
{
    public class VectorChunk:TextChunk
    {
        
        public float Similarity { get; set; }
        public float[] Embedding { get; set; } // store vector representation
    }
}
