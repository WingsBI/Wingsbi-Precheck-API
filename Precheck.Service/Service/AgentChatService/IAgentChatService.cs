using Precheck.Models.DTOs.Chatbot;

namespace Precheck.Service.Service.AgentChatService
{
    public interface IAgentChatService
    {
        Task<long> SaveTurnAsync(int userId, Guid sessionId, string request, string? response, string? toolTrace, int? inputTokens, int? outputTokens);
        Task<AgentChatPageDto> GetSessionPageAsync(int userId, Guid sessionId, int limit, long? beforeId);
    }
}
