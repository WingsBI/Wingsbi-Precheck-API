using Microsoft.Extensions.AI;
using OpenAI.Chat;
using Precheck.Agent;
using Precheck.Models.DTOs.Chatbot;

namespace Precheck.Host.Agents
{
    // Follow-up questions for the AG-UI (Copilot) flow: up to 3, based on the user's role domain.
    public class SuggestedQuestionsProvider : ISuggestedQuestionsProvider
    {
        private readonly ChatbotAgentFactory _agentFactory;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<SuggestedQuestionsProvider> _logger;

        public SuggestedQuestionsProvider(
            ChatbotAgentFactory agentFactory,
            IHttpContextAccessor httpContextAccessor,
            ILogger<SuggestedQuestionsProvider> logger)
        {
            _agentFactory = agentFactory;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task<IReadOnlyList<string>> GetAsync(string question, string answer)
        {
            try
            {
                int.TryParse(_httpContextAccessor.HttpContext?.User.FindFirst("roleid")?.Value, out var roleId);
                var domainDescription = ChatbotRoles.GetDomainDescription(roleId);
                IChatClient chatClient = _agentFactory.BuildChatClient().AsIChatClient();

                var messages = new List<Microsoft.Extensions.AI.ChatMessage>
                {
                    new(ChatRole.System,
                        $"The user's role domain is: {domainDescription}. Given the exchange below, suggest up to 3 " +
                        "short, relevant follow-up questions specific to that role's domain. Return only the structured result."),
                    new(ChatRole.User, $"Question: {question}\nAnswer: {answer}")
                };

                var result = await chatClient.GetResponseAsync<SuggestedQuestionsResult>(messages);
                return result.Result.Questions.Take(3).ToList();
            }
            catch (Exception ex)
            {
                // A failure here (e.g. rate limit) must never break the answer that already streamed.
                _logger.LogError(ex, "Failed to generate follow-up questions.");
                return new List<string>();
            }
        }
    }
}
