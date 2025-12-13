using AIHousingAssistant.Application.Services.Interfaces;
using AIHousingAssistant.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel.ChatCompletion;
using Newtonsoft.Json.Linq;
using System;

namespace AIHousingAssistant.Application.Services
{
    public class HousingService : IHousingService
    {
        private readonly HousingDbContext _context;

        public HousingService(HousingDbContext context)
        {
            _context = context;
        }

        public async Task<int> GetAvailableUnitsCount(string type)
        {
            return await _context.HousingUnits
                .Where(h => h.UnitType == type && h.IsAvailable)
                .CountAsync();
        }

        public async Task<bool> IsUnitTypeAvailable(string type)
        {
            return await _context.HousingUnits
                .AnyAsync(h => h.UnitType == type && h.IsAvailable);
        }

        public async Task<Dictionary<string, int>> GetAllAvailableGrouped()
        {
            return await _context.HousingUnits
                .Where(h => h.IsAvailable)
                .GroupBy(h => h.UnitType)
                .ToDictionaryAsync(g => g.Key, g => g.Count());
        }
    
        public async Task<List<string>> GetInitialQuestionsAsync()
        {
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Data", "HousingInitialQuestions.json");

            if (!File.Exists(filePath))
                return new List<string>(); // أو ترمي استثناء حسب رغبتك

            var jsonContent = await File.ReadAllTextAsync(filePath);

            if (string.IsNullOrWhiteSpace(jsonContent))
                return new List<string>();

            var token = JToken.Parse(jsonContent);

            if (token.Type != JTokenType.Array)
                return new List<string>();

            var questions = token.ToObject<List<string>>();
            return questions ?? new List<string>();
        }
    }
}