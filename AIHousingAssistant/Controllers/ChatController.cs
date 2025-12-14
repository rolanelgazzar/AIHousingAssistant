using AIHousingAssistant.Application.Enum;
using AIHousingAssistant.semantic.Plugins;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel;
using AIHousingAssistant.Models.Settings;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using System.Linq;
using Microsoft.SemanticKernel.Services;
using AIHousingAssistant.Application.Services.Interfaces;
using AIHousingAssistant.Application.SemanticKernel;
using AIHousingAssistant.Models;

namespace AIHousingAssistant.Controllers
{
    public class ChatController : Controller
    {
        private readonly IHousingService _housingService;
        private readonly IChatHistoryService _chatHistoryService;
        private readonly ProviderSettings _providerSettings;
        private readonly ISummarizerService _summarizer;
        private readonly IRagService _ragService;

        public ChatController(IHousingService housingService, IChatHistoryService chatHistoryService,
            IOptions<ProviderSettings> providerSettings
            , ISummarizerService summarizer
            , IRagService ragService)
            

        {
            _housingService = housingService;
            _chatHistoryService = chatHistoryService;
            _providerSettings = providerSettings.Value;
            _summarizer = summarizer;
            _ragService = ragService;

        }
        public IActionResult Index()
        {
            return View();
        }
        [HttpPost("AskChatAI_Planner")]
        public async Task<IActionResult> AskChatAI_Planner([FromBody] string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return BadRequest("Query cannot be empty.");

            try
            {
                // ---------------------------
                // 1. Build the Kernel and add the HousingPlugin
                // ---------------------------
                var kernelBuilder = SemanticKernelHelper.BuildKernel(AIProvider.OpenRouter, _providerSettings);
                SemanticKernelHelper.AddPlugin(kernelBuilder, new HousingPlugin(_housingService));
                var kernel = SemanticKernelHelper.Build(kernelBuilder);

                // ---------------------------
                // 2. Get or create semantic session
                // ---------------------------
                string sessionId = GetOrCreateSessionId();

                var history = _chatHistoryService.GetOrCreateHistory(sessionId);

                // ---------------------------
                // 3. Save user's message
                // ---------------------------
                _chatHistoryService.AddUserMessage(sessionId, query);

                // ---------------------------
                // 4. Get default prompt execution settings based on the AI provider
                // ---------------------------
                var settings = new OpenAIPromptExecutionSettings
                {
                    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
                };

                var result = await kernel.InvokePromptAsync(query, new(settings));
                string botResponse = result.GetValue<string>();
                ///----------------

                // ---------------------------
                // 5. Save bot's response
                // ---------------------------
                _chatHistoryService.AddAssistantMessage(sessionId, botResponse);

                return Ok(new { Success = true, Data = botResponse });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Error = ex.Message });
            }
        }

        [HttpPost("AskChatAI")]
        public async Task<IActionResult> AskChatAI([FromBody] string query)
        {
            /*
             "The Planner feature in Semantic Kernel has been deprecated in the current SDK version we are using. Instead of kernel.Planning or kernel.CreatePlanner(), we now rely on direct function invocation via Plugins and InvokePromptAsync. Any multi-step reasoning or task sequences need to be implemented manually in the code."
             */
            if (string.IsNullOrWhiteSpace(query))
                return BadRequest("Query cannot be empty.");

            try
            {
                // 1. Build the Kernel and add the HousingPlugin
                var kernelBuilder = SemanticKernelHelper.BuildKernel(AIProvider.OpenRouter, _providerSettings);
                SemanticKernelHelper.AddPlugin(kernelBuilder, new HousingPlugin(_housingService));
                var kernel = SemanticKernelHelper.Build(kernelBuilder);

                // 2. Get or create semantic session
                string sessionId = GetOrCreateSessionId();
              
                var history = _chatHistoryService.GetOrCreateHistory(sessionId);

                // 3. Save user's message
                _chatHistoryService.AddUserMessage(sessionId, query);

                string functionName = DetermineSkill(query);
                string unitType = ExtractUnitType(query);

                var function = kernel.Plugins.GetFunction("HousingPlugin", functionName);
                var result = await function.InvokeAsync(kernel, new KernelArguments
                {
                    ["type"] = unitType
                });

                string botResponse = result.GetValue<string>();

                
                return Ok(new { Success = true, Data = botResponse });
                
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Error = ex.Message });
            }
        }
        private string ExtractUnitType(string query)
        {
            if (query.Contains("شقة") || query.Contains("شقق")) return "شقة";
            if (query.Contains("فيلا") || query.Contains("فلل")) return "فيلا";
            return "وحدة";
        }

        private string DetermineSkill(string query)
        {
            if (query.Contains("كم") || query.Contains("عدد")) return "GetAvailableUnitsCountAsync";
            if (query.Contains("هل") || query.Contains("موجود")) return "CheckAvailabilityAsync";
            return "GetAllAvailableGroupedAsync"; // fallback if no specific match
        }

        #region Normal API
        [HttpGet("GetInitialQuestions")]
        public async Task<IActionResult> GetInitialQuestions()
        {
            try
            {
                var questions = await _housingService.GetInitialQuestionsAsync();

                if (questions.Count == 0)
                    return NotFound(new { success = false, error = "No initial questions found." });

                return Ok(new { success = true, data = questions });
            }
            catch (JsonReaderException ex)
            {
                return BadRequest(new { success = false, error = $"Error parsing JSON: {ex.Message}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = $"An error occurred: {ex.Message}" });
            }
        }
        #endregion

        #region  History
        [HttpGet("SummarizationChatHistory")]
        public async Task<IActionResult> SummarizationChatHistory()
        {
            // 2. Get or create semantic session
            var sessionId = GetOrCreateSessionId();
          

            if (string.IsNullOrWhiteSpace(sessionId))
                return BadRequest(new { success = false, error = "No active session" });
            ///set history to test
            //var history = _chatHistoryService.GetOrCreateHistory(sessionId);

            //    _chatHistoryService.AddUserMessage(sessionId, "test test test test ");
            /////////////////////
    
                var history = _chatHistoryService.GetChatHistory(sessionId);

            if (history == null)
                return NotFound(new { success = false, error = "No history found" });

            var summary = await _summarizer.SummarizeChatAsync(history);

            return Ok(new { success = true, summary });
        }

        private string GetOrCreateSessionId()
        {
            var id = HttpContext.Session.GetString("SessionId");

            if (string.IsNullOrEmpty(id))
            {
                id = Guid.NewGuid().ToString();
                HttpContext.Session.SetString("SessionId", id);
            }

            return id;
        }


        #endregion
        #region upload
        [HttpPost("UploadDocument")]
        public async Task<IActionResult> UploadDocument(IFormFile file, [FromForm] string config)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { success = false, error = "No file uploaded" });

            try
            {
                RagUiRequest request = JsonConvert.DeserializeObject<RagUiRequest>(config);

                await _ragService.ProcessDocumentAsync(file, request);

                return Ok(new
                {
                    success = true,
                    fileName = file.FileName,
                    message = "File uploaded and RAG processing started."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }


        #endregion
        [HttpPost("AskRag")]
        public async Task<IActionResult> AskRagAsync([FromBody] RagUiRequest ragRequest)
        {
            var reply = await _ragService.AskRagAsync(ragRequest);
            return Ok(new { data = reply });
        }

       


    }
}
