using AIHousingAssistant.Application.Enum;
using AIHousingAssistant.Models;
using Microsoft.AspNetCore.Mvc;

namespace AIHousingAssistant.Application.Services.Interfaces.Tools
{
    public interface IRagService
    {
        public Task ProcessDocumentByRagAsync(
               List<IFormFile> file,
               RagUiRequest ragUiRequest
               );
  

        public Task<RagAnswerResponse> AskRagAsync(RagUiRequest ragRequest);




    }
}
