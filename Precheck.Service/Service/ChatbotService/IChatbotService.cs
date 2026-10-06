using Precheck.Models.DataModel.Chatbot;
using Precheck.Models.DTOs.Chatbot;

namespace Precheck.Service.Service.ChatbotService
{
    public interface IChatbotService
    {
        Task<int> GetNewSessionIdAsync();
        Task<bool> SessionBelongsToUserAsync(int sessionId, int userId);
        Task AddHistoryAsync(int userId, int sessionId, string request, string response);
        Task<List<ChatSessionRecord>> GetHistoryAsync(int sessionId);
        Task<PreviousConversationsResponseDto> GetPreviousConversationsAsync(int userId, int? currentSessionId, int? cursor, int pageSize);
    }
}
