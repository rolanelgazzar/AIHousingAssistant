using AIHousingAssistant.Models;

namespace AIHousingAssistant.Application.Services.Interfaces.Tools
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
