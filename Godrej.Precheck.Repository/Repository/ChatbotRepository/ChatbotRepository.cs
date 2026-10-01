using Godrej.Precheck.Models.DataModel.Chatbot;
using Godrej.Precheck.Repository.Database;
using Godrej.Precheck.Repository.Queries;
using Microsoft.Extensions.Logging;

namespace Godrej.Precheck.Repository.Repository.ChatbotRepository
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

        public async Task<int> CreateSessionAsync(int userId, string? title)
        {
            _logger.LogInformation("Request for ChatbotRepository:CreateSessionAsync, UserId: {UserId}", userId);
            try
            {
                var sessionId = await _db.ExecuteScalar<int>(
                    ChatbotQueries.INSERT_CHAT_SESSION,
                    new { UserId = userId, Title = title });

                return sessionId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating chat session for UserId: {UserId}", userId);
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

        public async Task<string?> GetSessionStateAsync(int sessionId)
        {
            try
            {
                return await _db.GetSingle<string?>(
                    ChatbotQueries.GET_SESSION_STATE,
                    new { SessionId = sessionId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching session state for SessionId: {SessionId}", sessionId);
                throw;
            }
        }

        public async Task UpdateSessionStateAsync(int sessionId, string sessionState)
        {
            try
            {
                await _db.Execute(
                    ChatbotQueries.UPDATE_SESSION_STATE,
                    new { SessionId = sessionId, SessionState = sessionState });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating session state for SessionId: {SessionId}", sessionId);
                throw;
            }
        }

        public async Task AddMessageAsync(int sessionId, string role, string content, string? toolCalled)
        {
            try
            {
                await _db.Execute(
                    ChatbotQueries.INSERT_CHAT_MESSAGE,
                    new { SessionId = sessionId, Role = role, Content = content, ToolCalled = toolCalled });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding chat message for SessionId: {SessionId}", sessionId);
                throw;
            }
        }

        public async Task<List<ChatMessageRecord>> GetMessagesAsync(int sessionId)
        {
            try
            {
                var results = await _db.GetAll<ChatMessageRecord>(
                    ChatbotQueries.GET_MESSAGES_BY_SESSION,
                    new { SessionId = sessionId });

                return results.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching chat messages for SessionId: {SessionId}", sessionId);
                throw;
            }
        }
    }
}
