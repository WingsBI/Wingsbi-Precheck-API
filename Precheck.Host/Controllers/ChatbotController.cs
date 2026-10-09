using Precheck.Host.Agents;
using Precheck.Host.Helpers;
using Precheck.Models.DTOs.Chatbot;
using Precheck.Service.Service.AgentChatService;
using Precheck.Service.Service.ChatbotService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Precheck.Host.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatbotController : ControllerBase
    {
        private const int PreviousConversationsPageSize = 5;

        private readonly ILogger<ChatbotController> _logger;
        private readonly IChatbotService _chatbotService;
        private readonly IAgentChatService _agentChatService;
        private readonly ChatUploadStore _uploads;

        public ChatbotController(
            ILogger<ChatbotController> logger,
            IChatbotService chatbotService,
            IAgentChatService agentChatService,
            ChatUploadStore uploads)
        {
            _logger = logger;
            _chatbotService = chatbotService;
            _agentChatService = agentChatService;
            _uploads = uploads;
        }

        // Step 1 of attaching a file in the chat: the frontend posts the file here, gets a fileId back, and sends
        // attachmentNote as part of the user's next chat message. The agent then asks what to do with the file and
        // calls the matching tool (see ChatbotFileTools). Nothing is read or imported at this point.
        [HttpPost("UploadFile")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UploadFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "No file uploaded" });
            }

            var fileError = ExcelFileValidator.Validate(file);
            if (fileError != null)
            {
                return BadRequest(new { message = fileError });
            }

            try
            {
                var userId = Convert.ToInt32(User.FindFirst("id")?.Value);
                var saved = await _uploads.SaveAsync(file, userId);

                return Ok(new
                {
                    fileId = saved.FileId,
                    fileName = saved.FileName,
                    sizeBytes = saved.SizeBytes,
                    attachmentNote = $"[Attached file \"{saved.FileName}\" - fileId: {saved.FileId}]"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception Error for ChatbotController:UploadFile");
                return StatusCode(500, new { message = "Unable to save the file." });
            }
        }

        // Called after a page refresh: the frontend sends the session id it kept and gets back the last
        // messages of that session (display only). A Guid is a Copilot session and continues with the same id.
        [HttpPost("GetSessionBySessionId")]
        [Authorize]
        [ProducesResponseType(typeof(SessionMessagesResponseDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetSessionBySessionId([FromBody] GetSessionRequestDto request)
        {
            var isGuid = Guid.TryParse(request?.SessionId, out var guidSessionId);
            var isLegacy = int.TryParse(request?.SessionId, out var legacySessionId) && legacySessionId > 0;
            if (request == null || (!isGuid && !isLegacy))
            {
                return BadRequest(new { message = "A valid sessionId is required." });
            }

            try
            {
                var userId = Convert.ToInt32(User.FindFirst("id")?.Value);

                // A Guid is a Copilot (AG-UI) session; a numeric id is a legacy int session.
                SessionMessagesResponseDto? result;
                if (isGuid)
                {
                    result = await _agentChatService.GetSessionMessagesAsync(userId, guidSessionId, PreviousConversationsPageSize);
                }
                else
                {
                    result = await _chatbotService.GetSessionBySessionIdAsync(legacySessionId, userId, PreviousConversationsPageSize);
                    if (result != null)
                    {
                        result.SessionId = result.NewSessionId?.ToString() ?? string.Empty;
                    }
                }

                if (result == null)
                {
                    return NotFound(new { message = "Session not found with this session id." });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception Error for ChatbotController:GetSessionBySessionId");
                return StatusCode(500, new { message = "Unable to load the session." });
            }
        }

        // Recommended questions for the logged-in user's role, shown when a chat is opened. No LLM call.
        [HttpGet("RecommendedQuestions")]
        [Authorize]
        public IActionResult GetRecommendedQuestions()
        {
            var roleId = Convert.ToInt32(User.FindFirst("roleid")?.Value ?? "0");
            return Ok(new { suggestedQuestions = ChatbotRoles.GetStarterQuestions(roleId) });
        }
    }
}
