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
                        "short follow-up requests specific to that role's domain. Each one will be sent as the user's own " +
                        "next message when clicked, so write it from the user's point of view as a direct, self-contained " +
                        "command or question the assistant can answer immediately. Use simple everyday words and vary the " +
                        "wording so the suggestions start with different words and not all with \"Show\" (e.g. \"Give me " +
                        "the pending production orders\", \"How many QR codes are available?\", \"List the pending " +
                        "prechecks\"). Never address the user (no \"Do you want...\" or \"Would you like...\"), and never " +
                        "suggest something that needs an identifier the user hasn't given (like a specific PO or QR number). " +
                        "Return only the structured result."),
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
