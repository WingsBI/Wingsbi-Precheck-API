using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.AI;
using Precheck.Service.Service.AgentChatService;

namespace Precheck.Agent
{
    // Builds what the model actually sees: the last N saved turns of the session (question and answer text
    // only, never old tool payloads) followed by the new question. The browser's own history is ignored.
    // Without a valid session header the model gets the new question alone.
    public class AgentContextBuilder
    {
        private const int DefaultTurns = 10;

        private readonly IAgentChatService _agentChatService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<AgentContextBuilder> _logger;
        private readonly int _turns;

        public AgentContextBuilder(
            IAgentChatService agentChatService,
            IHttpContextAccessor httpContextAccessor,
            IConfiguration configuration,
            ILogger<AgentContextBuilder> logger)
        {
            _agentChatService = agentChatService;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            _turns = int.TryParse(configuration["AgentSettings:ContextTurns"], out var n) && n > 0 ? n : DefaultTurns;
        }

        public async Task<List<ChatMessage>> BuildAsync(IReadOnlyList<ChatMessage> received)
        {
            var newest = received.LastOrDefault(m => m.Role == ChatRole.User);
            if (newest is null) return received.ToList();

            var context = new List<ChatMessage>();

            var user = _httpContextAccessor.HttpContext?.User;
            if (AgentSessionHeader.TryGet(_httpContextAccessor, out var sessionId)
                && int.TryParse(user?.FindFirst("id")?.Value, out var userId))
            {
                try
                {
                    var page = await _agentChatService.GetSessionPageAsync(userId, sessionId, _turns, null);

                    foreach (var turn in page.Items)
                    {
                        if (string.IsNullOrWhiteSpace(turn.Response)) continue;
                        context.Add(new ChatMessage(ChatRole.User, turn.Request));
                        context.Add(new ChatMessage(ChatRole.Assistant, turn.Response));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Could not load chat history for session {SessionId}; continuing without it.", sessionId);
                }
            }

            context.Add(newest);
            return context;
        }
    }
}
