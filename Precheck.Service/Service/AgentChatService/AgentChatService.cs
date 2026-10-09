using Precheck.Models.DataModel.Chatbot;
using Precheck.Models.DTOs.Chatbot;
using Precheck.Repository.Repository.AgentChatRepository;

namespace Precheck.Service.Service.AgentChatService
{
    public class AgentChatService : IAgentChatService
    {
        private readonly IAgentChatRepository _repository;

        public AgentChatService(IAgentChatRepository repository)
        {
            _repository = repository;
        }

        public Task<long> SaveTurnAsync(int userId, Guid sessionId, string request, string? response, string? toolTrace, int? inputTokens, int? outputTokens)
            => _repository.SaveMessageAsync(new AgentChatMessageModel
            {
                SessionId = sessionId,
                Request = request,
                Response = response,
                ToolTrace = toolTrace,
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                CreatedBy = userId
            });

        public async Task<AgentChatPageDto> GetSessionPageAsync(int userId, Guid sessionId, int limit, long? beforeId)
        {
            limit = Math.Clamp(limit, 1, 50);

            // One extra row tells us whether an older page exists without a second query.
            var rows = await _repository.GetPageNewestFirstAsync(sessionId, userId, beforeId, limit + 1);
            var hasMore = rows.Count > limit;
            var page = rows.Take(limit).Reverse().ToList();

            return new AgentChatPageDto
            {
                Items = page,
                HasMore = hasMore,
                NextCursor = hasMore && page.Count > 0 ? page[0].Id : null
            };
        }

        public async Task<SessionMessagesResponseDto?> GetSessionMessagesAsync(int userId, Guid sessionId, int count)
        {
            var page = await GetSessionPageAsync(userId, sessionId, count, null);

            // No rows means the session doesn't exist or isn't owned by this user - indistinguishable on purpose.
            if (page.Items.Count == 0)
            {
                return null;
            }

            return new SessionMessagesResponseDto
            {
                SessionId = sessionId.ToString(),
                Messages = page.Items
                    .Select(i => new ChatSessionItemDto
                    {
                        Id = (int)i.Id,
                        Request = i.Request,
                        Response = i.Response ?? string.Empty,
                        CreatedDate = i.CreatedDate
                    })
                    .ToList()
            };
        }
    }
}
