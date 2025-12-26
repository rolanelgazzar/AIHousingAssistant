using Microsoft.AspNetCore.Mvc;
using AIHousingAssistant.Application.Enum;
using AIHousingAssistant.Application.Services.Interfaces;
using AIHousingAssistant.Application.Services.Interfaces.Tools;
using AIHousingAssistant.Models;
using AIHousingAssistant.Models.Settings;
using Microsoft.Extensions.Options;

namespace AIHousingAssistant.Controllers
{
    public class ChatController : Controller
    {
        // Core services
        private readonly IHousingService _housingService;
        private readonly IChatHistoryService _chatHistoryService;
        private readonly ISummarizerService _summarizer;

        // Tool-specific interfaces
        private readonly IMemoryKernelService _kernelMemoryService;
        private readonly IDirectChatService _directChatService;
        private readonly IPluginDbService _pluginDbService;
        private readonly IRagService _ragService;
        private readonly IWebSearchService _webSearchService;

        public ChatController(
            IHousingService housingService,
            IChatHistoryService chatHistoryService,
            ISummarizerService summarizer,
            IMemoryKernelService kernelMemoryService,
            IDirectChatService directChatService,
            IPluginDbService pluginDbService,
            IRagService ragService,
            IWebSearchService webSearchService)
        {
            _housingService = housingService;
            _chatHistoryService = chatHistoryService;
            _summarizer = summarizer;
            _kernelMemoryService = kernelMemoryService;
            _directChatService = directChatService;
            _pluginDbService = pluginDbService;
            _ragService = ragService;
            _webSearchService = webSearchService;
        }

        public IActionResult Index() => View();

        #region AI Chat Endpoints (Unified Request)

        [HttpPost("AskChatAI")]
        public async Task<IActionResult> AskChatAI([FromBody] RagUiRequest ragRequest)
        {
            ragRequest.AIProvider = AIProvider.OpenRouter;
            return await ExecuteAskAction(() => _directChatService.AskDirectChatAsync(ragRequest), ragRequest);
        }

        [HttpPost("AskRag")]
        public async Task<IActionResult> AskRagAsync([FromBody] RagUiRequest ragRequest)
        {
            return await ExecuteAskAction(() => _ragService.AskRagAsync(ragRequest), ragRequest);
        }

        [HttpPost("AskKernelMemory")]
        public async Task<IActionResult> AskKernelMemoryAsync([FromBody] RagUiRequest ragRequest)
        {
            return await ExecuteAskAction(() => _kernelMemoryService.AskMemoryKernelAsync(ragRequest), ragRequest);
        }

        [HttpPost("AskPluginDB")]
        public async Task<IActionResult> AskPluginDBAsync([FromBody] RagUiRequest ragRequest)
        {
            ragRequest.AIProvider = AIProvider.OpenRouter;
            return await ExecuteAskAction(() => _pluginDbService.AskPluginDBAsync(ragRequest), ragRequest);
        }

        [HttpPost("AskWeb")]
        public async Task<IActionResult> AskWebAsync([FromBody] RagUiRequest ragRequest)
        {
            ragRequest.AIProvider = AIProvider.OpenRouter;
            return await ExecuteAskAction(() => _webSearchService.AskWebAsync(ragRequest), ragRequest);
        }

        #endregion

        #region Document Management

        [HttpPost("UploadDocument")]
        public async Task<IActionResult> UploadDocument([FromForm] List<IFormFile> files, [FromForm] RagUiRequest request)
        {
            if (files == null || files.Count == 0)
                return BadRequest(new { success = false, error = "No files uploaded" });

            try
            {
                // Assign current SessionId before processing to ensure tags match during search
                request.SessionId = GetOrCreateSessionId();

                if (request.ToolsSearchBy == SearchToolType.KernelMemory)
                {
                    await _kernelMemoryService.ProcessDocumentByKernelMemoryAsync(files, request);
                }
                else if (request.ToolsSearchBy == SearchToolType.Rag)
                {
                    await _ragService.ProcessDocumentByRagAsync(files, request);
                }
                else
                {
                    return BadRequest(new
                    {
                        success = false,
                        error = $"Document upload is not supported for the selected tool: {request.ToolsSearchBy}. Please select RAG or Kernel Memory."
                    });
                }

                return Ok(new
                {
                    success = true,
                    fileCount = files.Count,
                    sessionId = request.SessionId,
                    message = $"Successfully processed {files.Count} documents."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        #endregion

        #region Helper APIs (Questions & History)

        [HttpGet("GetInitialQuestions")]
        public async Task<IActionResult> GetInitialQuestions()
        {
            try
            {
                var questions = await _housingService.GetInitialQuestionsAsync();
                return Ok(new { success = true, data = questions });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        [HttpGet("SummarizationChatHistory")]
        public async Task<IActionResult> SummarizationChatHistory()
        {
            var sessionId = GetOrCreateSessionId();
            var history = _chatHistoryService.GetChatHistory(sessionId);

            if (history == null)
                return NotFound(new { success = false, error = "No history found" });

            var summary = await _summarizer.SummarizeChatAsync(history);
            return Ok(new { success = true, summary });
        }

        #endregion

        #region Private Methods

        private async Task<IActionResult> ExecuteAskAction<T>(Func<Task<T>> action, RagUiRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Query))
                return BadRequest(new { error = "Request or Query cannot be empty." });

            try
            {
                // Ensure the search request uses the same session as the upload
                request.SessionId = GetOrCreateSessionId();

                var result = await action();

                return Ok( result );
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "An error occurred during processing.", details = ex.Message });
            }
        }

        private string GetOrCreateSessionId()
        {
            return "1";
            var id = HttpContext.Session.GetString("SessionId");
            if (string.IsNullOrEmpty(id))
            {
                id = Guid.NewGuid().ToString();
                HttpContext.Session.SetString("SessionId", id);
            }
            return id;
        }

        #endregion
    }
}