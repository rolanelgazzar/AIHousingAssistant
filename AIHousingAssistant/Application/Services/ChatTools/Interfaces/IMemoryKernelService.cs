using AIHousingAssistant.Models;

namespace AIHousingAssistant.Application.Services.ChatTools.Interfaces
{
    public interface IMemoryKernelService
    {

        public Task ProcessDocumentByKernelMemoryAsync(
             List<IFormFile> file,
             RagUiRequest ragUiRequest
             );
        public Task<RagAnswerResponse> AskMemoryKernelAsync(RagUiRequest ragRequest);

    }
}
