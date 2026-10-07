using System.Text.Json;
using Azure;
using Azure.AI.OpenAI;
using Precheck.Host.Agents;
using Precheck.Models.DTOs.Chatbot;
using Precheck.Service.Service.ChatbotService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace Precheck.Host.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatbotController : ControllerBase
    {
        private const int PreviousConversationsPageSize = 5;

        private readonly ILogger<ChatbotController> _logger;
        private readonly ChatbotAgentFactory _agentFactory;
        private readonly IChatbotService _chatbotService;

        public ChatbotController(
            ILogger<ChatbotController> logger,
            ChatbotAgentFactory agentFactory,
            IChatbotService chatbotService)
        {
            _logger = logger;
            _agentFactory = agentFactory;
            _chatbotService = chatbotService;
        }

        [HttpPost("AskStream")]
        //[Authorize]
        public async Task AskStream([FromBody] ChatRequestDto request)
        {
            if (request == null)
            {
                Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            var userId = Convert.ToInt32(User.FindFirst("id")?.Value);
            var roleId = Convert.ToInt32(User.FindFirst("roleid")?.Value ?? "0");

            _logger.LogInformation("Request for ChatbotController:AskStream: {Message}", request.Message);

            var agent = _agentFactory.BuildAgent();
            var (sessionId, session, messages) = await LoadOrCreateSessionAsync(agent, request, userId);
            if (session == null)
            {
                _logger.LogWarning("User {UserId} attempted to access chat session {SessionId} they do not own", userId, sessionId);
                Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            Response.Headers["Content-Type"] = "text/event-stream";
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["X-Accel-Buffering"] = "no";

            // Message is optional: with nothing to ask, skip the LLM and nothing is saved -
            // just hand back the session id so the client can start/continue the chat.
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                var emptyDonePayload = JsonSerializer.Serialize(new
                {
                    sessionId,
                    createdDate = DateTime.UtcNow,
                    suggestedQuestions = ChatbotRoles.GetStarterQuestions(roleId)
                });
                await Response.WriteAsync($"event: done\ndata: {emptyDonePayload}\n\n", HttpContext.RequestAborted);
                await Response.Body.FlushAsync(HttpContext.RequestAborted);
                return;
            }

            var updates = new List<AgentResponseUpdate>();
            try
            {
                await foreach (var update in agent.RunStreamingAsync(messages, session, cancellationToken: HttpContext.RequestAborted))
                {
                    updates.Add(update);
                    if (!string.IsNullOrEmpty(update.Text))
                    {
                        var payload = JsonSerializer.Serialize(new { text = update.Text });
                        await Response.WriteAsync($"data: {payload}\n\n", HttpContext.RequestAborted);
                        await Response.Body.FlushAsync(HttpContext.RequestAborted);
                    }
                }

                var response = updates.ToAgentResponse();

                var toolCalled = response.Messages
                    .SelectMany(m => m.Contents)
                    .OfType<FunctionCallContent>()
                    .Select(c => c.Name)
                    .FirstOrDefault();

                object? data = response.Messages
                    .SelectMany(m => m.Contents)
                    .OfType<FunctionResultContent>()
                    .Select(c => c.Result)
                    .FirstOrDefault();

                await _chatbotService.AddHistoryAsync(userId, sessionId, request.Message, response.Text);

                var suggestedQuestions = await GetSuggestedQuestionsAsync(roleId, request.Message, response.Text);

                var donePayload = JsonSerializer.Serialize(new
                {
                    sessionId,
                    toolCalled,
                    createdDate = DateTime.UtcNow,
                    data,
                    suggestedQuestions,
                    inputTokens = response.Usage?.InputTokenCount,
                    outputTokens = response.Usage?.OutputTokenCount,
                    totalTokens = response.Usage?.TotalTokenCount
                });
                await Response.WriteAsync($"event: done\ndata: {donePayload}\n\n", HttpContext.RequestAborted);
                await Response.Body.FlushAsync(HttpContext.RequestAborted);

                _logger.LogInformation("Response for ChatbotController:AskStream: SessionId={SessionId}, ToolCalled={ToolCalled}", sessionId, toolCalled);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception Error for ChatbotController:AskStream");

                // Headers (200, text/event-stream) are already sent by this point, so an HTTP error
                // status is no longer possible - surface the failure as an SSE "error" event instead,
                // and skip persistence so a broken/partial turn doesn't get saved into session history.
                var errorPayload = JsonSerializer.Serialize(new { message = "I couldn't reach the assistant right now, please try again shortly." });
                await Response.WriteAsync($"event: error\ndata: {errorPayload}\n\n", HttpContext.RequestAborted);
                await Response.Body.FlushAsync(HttpContext.RequestAborted);
            }
        }

        // Lean variant of AskStream: streams only the answer text (data: {"text"}) and the name of each tool
        // the model calls (event: tool). No tool payloads, suggestions or token counts.
        [HttpPost("AskLite")]
        [Authorize]
        public async Task AskLite([FromBody] ChatRequestDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Message))
            {
                Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            var userId = Convert.ToInt32(User.FindFirst("id")?.Value);

            _logger.LogInformation("Request for ChatbotController:AskLite: {Message}", request.Message);

            var agent = _agentFactory.BuildAgent();
            var (sessionId, session, messages) = await LoadOrCreateSessionAsync(agent, request, userId);
            if (session == null)
            {
                _logger.LogWarning("User {UserId} attempted to access chat session {SessionId} they do not own", userId, sessionId);
                Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            Response.Headers["Content-Type"] = "text/event-stream";
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["X-Accel-Buffering"] = "no";

            var updates = new List<AgentResponseUpdate>();
            try
            {
                await foreach (var update in agent.RunStreamingAsync(messages, session, cancellationToken: HttpContext.RequestAborted))
                {
                    updates.Add(update);

                    foreach (var call in update.Contents.OfType<FunctionCallContent>())
                    {
                        var toolPayload = JsonSerializer.Serialize(new { tool = call.Name });
                        await Response.WriteAsync($"event: tool\ndata: {toolPayload}\n\n", HttpContext.RequestAborted);
                        await Response.Body.FlushAsync(HttpContext.RequestAborted);
                    }

                    if (!string.IsNullOrEmpty(update.Text))
                    {
                        var payload = JsonSerializer.Serialize(new { text = update.Text });
                        await Response.WriteAsync($"data: {payload}\n\n", HttpContext.RequestAborted);
                        await Response.Body.FlushAsync(HttpContext.RequestAborted);
                    }
                }

                var response = updates.ToAgentResponse();
                await _chatbotService.AddHistoryAsync(userId, sessionId, request.Message, response.Text);

                // sessionId is the one extra field: the client needs it to continue the same conversation.
                var donePayload = JsonSerializer.Serialize(new { sessionId });
                await Response.WriteAsync($"event: done\ndata: {donePayload}\n\n", HttpContext.RequestAborted);
                await Response.Body.FlushAsync(HttpContext.RequestAborted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception Error for ChatbotController:AskLite");

                var errorPayload = JsonSerializer.Serialize(new { message = "I couldn't reach the assistant right now, please try again shortly." });
                await Response.WriteAsync($"event: error\ndata: {errorPayload}\n\n", HttpContext.RequestAborted);
                await Response.Body.FlushAsync(HttpContext.RequestAborted);
            }
        }

        // Called after a page refresh: the frontend sends the session id it kept in a cookie and gets back
        // the last messages of that session (display only) plus a new session id to continue chatting with.
        [HttpPost("GetSessionBySessionId")]
        [Authorize]
        [ProducesResponseType(typeof(SessionMessagesResponseDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetSessionBySessionId([FromBody] GetSessionRequestDto request)
        {
            if (request == null || request.SessionId <= 0)
            {
                return BadRequest(new { message = "A valid sessionId is required." });
            }

            try
            {
                var userId = Convert.ToInt32(User.FindFirst("id")?.Value);

                var result = await _chatbotService.GetSessionBySessionIdAsync(request.SessionId, userId, PreviousConversationsPageSize);
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

        private async Task<(int SessionId, AgentSession? Session, List<Microsoft.Extensions.AI.ChatMessage> Messages)> LoadOrCreateSessionAsync(AIAgent agent, ChatRequestDto request, int userId)
        {
            var messages = new List<Microsoft.Extensions.AI.ChatMessage>();
            int sessionId;

            if (request.SessionId == null)
            {
                sessionId = await _chatbotService.GetNewSessionIdAsync();
            }
            else
            {
                sessionId = request.SessionId.Value;
                var belongsToUser = await _chatbotService.SessionBelongsToUserAsync(sessionId, userId);
                if (!belongsToUser)
                {
                    return (sessionId, null, messages);
                }
            }

            // "Continue where I left off": the last few exchanges of the user's previous session are
            // given to the LLM as context (never saved against this session). Resolved relative to
            // this session's id so it stays the same on every turn of the new chat.
            var previous = await _chatbotService.GetPreviousConversationsAsync(userId, sessionId, null, PreviousConversationsPageSize);
            foreach (var h in previous.Messages)
            {
                messages.Add(new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, h.Request));
                messages.Add(new Microsoft.Extensions.AI.ChatMessage(ChatRole.Assistant, h.Response));
            }

            if (request.SessionId != null)
            {

                // Rebuild the conversation context from the stored request/response pairs.
                var history = await _chatbotService.GetHistoryAsync(sessionId);
                foreach (var h in history)
                {
                    messages.Add(new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, h.Request));
                    messages.Add(new Microsoft.Extensions.AI.ChatMessage(ChatRole.Assistant, h.Response));
                }
            }

            if (!string.IsNullOrWhiteSpace(request.Message))
            {
                messages.Add(new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, request.Message));
            }
            var session = await agent.CreateSessionAsync();
            return (sessionId, session, messages);
        }

        private async Task<List<string>> GetSuggestedQuestionsAsync(int roleId, string userMessage, string answerText)
        {
            try
            {
                var domainDescription = ChatbotRoles.GetDomainDescription(roleId);
                IChatClient chatClient = _agentFactory.BuildChatClient().AsIChatClient();

                var messages = new List<Microsoft.Extensions.AI.ChatMessage>
                {
                    new(ChatRole.System,
                        $"The user's role domain is: {domainDescription}. Given the exchange below, suggest up to 3 " +
                        "short, relevant follow-up questions specific to that role's domain. Return only the structured result."),
                    new(ChatRole.User, $"Question: {userMessage}\nAnswer: {answerText}")
                };

                var result = await chatClient.GetResponseAsync<SuggestedQuestionsResult>(messages);
                return result.Result.Questions.Take(3).ToList();
            }
            catch (Exception ex)
            {
                // A failure here (e.g. rate limit) should never break the main answer that already
                // succeeded - fall back to no suggestions rather than failing the whole request.
                _logger.LogError(ex, "Exception Error for ChatbotController:GetSuggestedQuestionsAsync");
                return new List<string>();
            }
        }
    }
}
