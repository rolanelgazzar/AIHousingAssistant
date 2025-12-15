using AIHousingAssistant.Application.Enum;
using AIHousingAssistant.Models;

namespace AIHousingAssistant.Application.Services.Chunk
{
    public interface IChunkService
    {
        public Task<List<TextChunk>> CreateChunksAsync(
                 string text,
                 RagUiRequest ragUiRequest,
                 string source);

    }
}
