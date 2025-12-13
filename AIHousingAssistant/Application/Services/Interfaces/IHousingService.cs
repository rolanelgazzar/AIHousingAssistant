using Microsoft.SemanticKernel.ChatCompletion;

namespace AIHousingAssistant.Application.Services.Interfaces
{
    public interface IHousingService
    {
        Task<int> GetAvailableUnitsCount(string type);
        Task<bool> IsUnitTypeAvailable(string type);
        Task<Dictionary<string, int>> GetAllAvailableGrouped();
        Task<List<string>> GetInitialQuestionsAsync();


    }
}
