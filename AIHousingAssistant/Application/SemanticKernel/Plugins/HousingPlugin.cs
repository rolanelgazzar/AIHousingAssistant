using AIHousingAssistant.Application.Services.Interfaces;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text;

namespace AIHousingAssistant.semantic.Plugins
{
    public class HousingPlugin
    {
        private readonly IHousingService _housingService;

        public HousingPlugin(IHousingService housingService)
        {
            _housingService = housingService;
        }

        [KernelFunction(nameof(HousingPlugin.CheckAvailabilityAsync)), Description(
            "Check if a specific unit type like 'شقة' or 'فيلا' is available. Use this if the user asks 'هل توجد شقة؟' or 'هل يوجد فيلا؟'")]
        public async Task<string> CheckAvailabilityAsync(string type)
        {
            var result = await _housingService.IsUnitTypeAvailable(type);
            return result
                ? $"نعم، يوجد {type} متاحة ✅"
                : $"عذرًا، لا توجد {type} حاليًا ❌";
        }

        [KernelFunction(nameof(HousingPlugin.GetAvailableUnitsCountAsync)), Description(
            "Return the count of available units of a certain type (like شقة or فيلا). Use if the user asks 'كم عدد الشقق؟' or 'ما عدد الفلل؟'")]
        public async Task<string> GetAvailableUnitsCountAsync(string type)
        {
            var count = await _housingService.GetAvailableUnitsCount(type);
            return count > 0
                ? $"عدد الوحدات المتاحة من نوع {type}: {count} وحدة ✅"
                : $"لا توجد وحدات متاحة من نوع {type} ❌";
        }

        [KernelFunction(nameof(HousingPlugin.GetAllAvailableGroupedAsync)), Description(
             "List all available unit types with counts. Use if the user asks 'ما هي الأنواع المتاحة؟' or 'اعرض جميع أنواع السكن المتوفرة'")]
        public async Task<string> GetAllAvailableGroupedAsync()
        {
            var grouped = await _housingService.GetAllAvailableGrouped();

            if (grouped.Count == 0)
                return "لا توجد أي وحدات متاحة حالياً ❌";

            var sb = new StringBuilder("الوحدات المتاحة حسب النوع:\n");

            foreach (var kvp in grouped)
            {
                sb.AppendLine($"- {kvp.Key}: {kvp.Value} وحدة");
            }

            return sb.ToString();
        }
    }
}
