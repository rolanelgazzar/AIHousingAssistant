using AIHousingAssistant.Application.Enum;
using AIHousingAssistant.Models;

namespace AIHousingAssistant.Application.Services.Chunk
{
    public interface IChunkService
    {
        Task<List<TextChunk>> CreateChunksAsync(string text, ChunkingMode chunkingMode, string source);
    }
}
