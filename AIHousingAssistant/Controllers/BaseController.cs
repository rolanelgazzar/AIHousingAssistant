using AIHousingAssistant.Application.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AIHousingAssistant.Controllers
{
    public class BaseController : Controller
    {

        protected readonly IChatHistoryService _chatHistoryService;
        protected string nafathId = "123456789";
        public BaseController(IChatHistoryService chatHistoryService)
        {

            _chatHistoryService = chatHistoryService;

        }

        ////[HttpGet("/GetInitialQuestions")]
        //[ApiExplorerSettings(IgnoreApi = true)]

        //public IActionResult GetInitialQuestions()
        //{
        //    try
        //    {
        //        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Data", "HousingInitialQuestions.json");

        //        if (!System.IO.File.Exists(filePath))
        //            return NotFound(new { success = false, error = "ملف الأسئلة المبدئية غير موجود." });

        //        var jsonContent = System.IO.File.ReadAllText(filePath);

        //        if (string.IsNullOrWhiteSpace(jsonContent))
        //            return BadRequest(new { success = false, error = "ملف الأسئلة المبدئية فارغ." });


        //        var token = JToken.Parse(jsonContent);

        //        if (token.Type != JTokenType.Array)
        //            return BadRequest(new { success = false, error = "صيغة ملف الأسئلة المبدئية غير صحيحة. يجب أن تكون Array." });

        //        var questions = token.ToObject<List<string>>();

        //        if (questions == null || questions.Count == 0)
        //            return BadRequest(new { success = false, error = "لم يتم العثور على أي أسئلة في الملف." });

        //        return Ok(new { success = true, data = questions });
        //    }
        //    catch (JsonReaderException ex)
        //    {
        //        return BadRequest(new { success = false, error = $"خطأ في قراءة ملف JSON: {ex.Message}" });
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, new { success = false, error = $"حدث خطأ أثناء معالجة الطلب: {ex.Message}" });
        //    }
        //}
        //[ApiExplorerSettings(IgnoreApi = true)]



        //public async Task<IActionResult> GetChatHistory()
        //{
        //    try
        //    {

        //        var history = await _chatHistoryService.GetHistoryAsync(nafathId);
        //        return Ok(new { success = true, data = history });
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, new { success = false, error = ex.Message });
        //    }
        //}
    }
}
