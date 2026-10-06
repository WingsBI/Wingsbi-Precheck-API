using Precheck.Models.DataModel.Chatbot;
using Precheck.Repository.Database;
using Precheck.Repository.Queries;
using Microsoft.Extensions.Logging;

namespace Precheck.Repository.Repository.ChatbotRepository
{
    public class ChatbotRepository : IChatbotRepository
    {
        private readonly ILogger<ChatbotRepository> _logger;
        private readonly IApplicationDbContext _db;

        public ChatbotRepository(ILogger<ChatbotRepository> logger, IApplicationDbContext db)
        {
            _logger = logger;
            _db = db;
        }

        public async Task<int> GetNewSessionIdAsync()
        {
            try
            {
                return await _db.ExecuteScalar<int>(ChatbotQueries.GET_NEXT_SESSION_ID, new { });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating new chat session id");
                throw;
            }
        }

        public async Task<bool> SessionBelongsToUserAsync(int sessionId, int userId)
        {
            try
            {
                var result = await _db.ExecuteScalar<int>(
                    ChatbotQueries.SESSION_BELONGS_TO_USER,
                    new { SessionId = sessionId, UserId = userId });

                return result == 1;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking session ownership for SessionId: {SessionId}", sessionId);
                throw;
            }
        }

        public async Task AddHistoryAsync(int userId, int sessionId, string request, string response)
        {
            try
            {
                await _db.Execute(
                    ChatbotQueries.INSERT_CHAT_HISTORY,
                    new { UserId = userId, SessionId = sessionId, Request = request, Response = response });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding chat history for SessionId: {SessionId}", sessionId);
                throw;
            }
        }

        public async Task<List<ChatSessionRecord>> GetHistoryAsync(int sessionId)
        {
            try
            {
                var results = await _db.GetAll<ChatSessionRecord>(
                    ChatbotQueries.GET_HISTORY_BY_SESSION,
                    new { SessionId = sessionId });

                return results.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching chat history for SessionId: {SessionId}", sessionId);
                throw;
            }
        }

        public async Task<List<ChatSessionRecord>> GetPreviousSessionHistoryAsync(int userId, int beforeSessionId, int? cursor, int pageSize)
        {
            try
            {
                var results = await _db.GetAll<ChatSessionRecord>(
                    ChatbotQueries.GET_PREVIOUS_SESSION_HISTORY,
                    new { UserId = userId, BeforeSessionId = beforeSessionId, Cursor = cursor, PageSize = pageSize });

                return results.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching previous session history for UserId: {UserId}", userId);
                throw;
            }
        }
    }
}
