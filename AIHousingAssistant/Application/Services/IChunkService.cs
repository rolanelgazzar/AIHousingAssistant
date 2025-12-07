using AIHousingAssistant.Models;

namespace AIHousingAssistant.Application.Services
{
    public interface IChunkService
    {
        Task<List<TextChunk>> CreateChunksAsync(string text);
    }
}
