namespace AIHousingAssistant.Models
{
    public class TextChunk
    {
        public int Index { get; set; }
        public string Content { get; set; }
        public string Source { get; set; }      // file name / url / doc id
        public int? Page { get; set; }           // optional for pdf

    }

}
