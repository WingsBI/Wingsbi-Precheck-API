using Precheck.Host.Agents;
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

        public ChatbotController(
            ILogger<ChatbotController> logger,
            IChatbotService chatbotService,
            IAgentChatService agentChatService)
        {
            _logger = logger;
            _chatbotService = chatbotService;
            _agentChatService = agentChatService;
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
