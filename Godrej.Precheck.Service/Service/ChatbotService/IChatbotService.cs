using Godrej.Precheck.Models.DataModel.Chatbot;

namespace Godrej.Precheck.Service.Service.ChatbotService
{
    public interface IChatbotService
    {
        Task<int> CreateSessionAsync(int userId, string? title);
        Task<bool> SessionBelongsToUserAsync(int sessionId, int userId);
        Task<string?> GetSessionStateAsync(int sessionId);
        Task UpdateSessionStateAsync(int sessionId, string sessionState);
        Task AddMessageAsync(int sessionId, string role, string content, string? toolCalled);
        Task<List<ChatMessageRecord>> GetMessagesAsync(int sessionId);
    }
}
