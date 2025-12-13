using AIHousingAssistant.Application.Enum;
using AIHousingAssistant.Models;

namespace AIHousingAssistant.Application.Services.Interfaces
{
    public interface IChunkService
    {
        Task<List<TextChunk>> CreateChunksAsync(string text, ChunkingMode chunkingMode, string source);
    }
}
