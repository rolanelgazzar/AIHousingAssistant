using AIHousingAssistant.Application.Services.Interfaces.Tools;
using AIHousingAssistant.Models;

namespace AIHousingAssistant.Application.Services
{
    public class MemoryKernelService : IMemoryKernelService
    {
        public Task<RagAnswerResponse> AskMemoryKernelAsync(RagUiRequest ragRequest)
        {
            throw new NotImplementedException();
        }

        public Task ProcessDocumentByKernelMemoryAsync(List<IFormFile> file, RagUiRequest ragUiRequest)
        {
            throw new NotImplementedException();
        }
    }
}
