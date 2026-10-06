using Precheck.Models.DataModel.Chatbot;
using Precheck.Models.DTOs.Chatbot;
using Precheck.Repository.Repository.ChatbotRepository;
using Microsoft.Extensions.Logging;

namespace Precheck.Service.Service.ChatbotService
{
    public class ChatbotService : IChatbotService
    {
        private readonly IChatbotRepository _chatbotRepository;
        private readonly ILogger<ChatbotService> _logger;

        public ChatbotService(IChatbotRepository chatbotRepository, ILogger<ChatbotService> logger)
        {
            _chatbotRepository = chatbotRepository;
            _logger = logger;
        }

        public Task<int> GetNewSessionIdAsync()
            => _chatbotRepository.GetNewSessionIdAsync();

        public Task<bool> SessionBelongsToUserAsync(int sessionId, int userId)
            => _chatbotRepository.SessionBelongsToUserAsync(sessionId, userId);

        public Task AddHistoryAsync(int userId, int sessionId, string request, string response)
            => _chatbotRepository.AddHistoryAsync(userId, sessionId, request, response);

        public Task<List<ChatSessionRecord>> GetHistoryAsync(int sessionId)
            => _chatbotRepository.GetHistoryAsync(sessionId);

        public async Task<PreviousConversationsResponseDto> GetPreviousConversationsAsync(int userId, int? currentSessionId, int? cursor, int pageSize)
        {
            // A null currentSessionId means a brand-new chat, so every existing session counts as "previous".
            var beforeSessionId = currentSessionId ?? int.MaxValue;

            // Fetch one extra row to learn whether older exchanges exist without a second query.
            var rows = await _chatbotRepository.GetPreviousSessionHistoryAsync(userId, beforeSessionId, cursor, pageSize + 1);

            var hasMore = rows.Count > pageSize;
            var page = rows.Take(pageSize).ToList(); // newest first

            return new PreviousConversationsResponseDto
            {
                HasMore = hasMore,
                NextCursor = hasMore ? page[^1].Id : null,
                Messages = page
                    .AsEnumerable()
                    .Reverse()
                    .Select(r => new ChatSessionItemDto
                    {
                        Id = r.Id,
                        Request = r.Request,
                        Response = r.Response,
                        CreatedDate = r.CreatedDate
                    })
                    .ToList()
            };
        }
    }
}
