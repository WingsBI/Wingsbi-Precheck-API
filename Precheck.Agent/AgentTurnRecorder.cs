using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Precheck.Service.Service.AgentChatService;

namespace Precheck.Agent
{
    // Saves one finished question/answer turn to tbl_agent_chat_sessions. The session comes from the X-Session-Id
    // request header, the user from the JWT. A failure here is logged and swallowed: losing a history row
    // must never break the chat reply.
    public class AgentTurnRecorder
    {
        private readonly IAgentChatService _agentChatService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<AgentTurnRecorder> _logger;

        public AgentTurnRecorder(
            IAgentChatService agentChatService,
            IHttpContextAccessor httpContextAccessor,
            ILogger<AgentTurnRecorder> logger)
        {
            _agentChatService = agentChatService;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task SaveAsync(IReadOnlyList<ChatMessage> received, string reply, IReadOnlyList<FunctionCallContent> toolCalls, long inputTokens, long outputTokens)
        {
            try
            {
                var question = received.LastOrDefault(m => m.Role == ChatRole.User)?.Text;
                if (string.IsNullOrWhiteSpace(question)) return;

                if (!AgentSessionHeader.TryGet(_httpContextAccessor, out var sessionId))
                {
                    _logger.LogWarning("Chat turn not saved: missing or invalid {Header} header.", AgentSessionHeader.Name);
                    return;
                }

                if (!int.TryParse(_httpContextAccessor.HttpContext?.User.FindFirst("id")?.Value, out var userId))
                {
                    _logger.LogWarning("Chat turn not saved: no authenticated user id.");
                    return;
                }

                var trace = toolCalls.Count == 0
                    ? null
                    : JsonSerializer.Serialize(toolCalls.Select(c => new { tool = c.Name, args = c.Arguments }));

                await _agentChatService.SaveTurnAsync(userId, sessionId, question, reply.Trim(), trace, (int)inputTokens, (int)outputTokens);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save chat turn.");
            }
        }
    }
}
