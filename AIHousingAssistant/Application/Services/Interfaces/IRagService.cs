using AIHousingAssistant.Application.Enum;
using AIHousingAssistant.Models;
using Microsoft.AspNetCore.Mvc;

namespace AIHousingAssistant.Application.Services.Interfaces
{
    public interface IRagService
    {
        public Task ProcessDocumentAsync(
               IFormFile file,
               RagUiRequest ragUiRequest
               );

        public Task<RagAnswerResponse> AskRagAsync(RagUiRequest ragRequest);
    }
}
