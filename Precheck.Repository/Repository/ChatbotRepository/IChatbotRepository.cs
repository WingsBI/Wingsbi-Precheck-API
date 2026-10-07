using Precheck.Models.DataModel.Chatbot;

namespace Precheck.Repository.Repository.ChatbotRepository
{
    public interface IChatbotRepository
    {
        Task<int> GetNewSessionIdAsync();
        Task<bool> SessionBelongsToUserAsync(int sessionId, int userId);
        Task AddHistoryAsync(int userId, int sessionId, string request, string response);
        Task<List<ChatSessionRecord>> GetHistoryAsync(int sessionId);
        Task<List<ChatSessionRecord>> GetLastSessionMessagesAsync(int sessionId, int userId, int count);
        Task<List<ChatSessionRecord>> GetPreviousSessionHistoryAsync(int userId, int beforeSessionId, int? cursor, int pageSize);
    }
}
