using Precheck.Models.DataModel.Chatbot;
using Precheck.Models.DTOs.Chatbot;

namespace Precheck.Repository.Repository.AgentChatRepository
{
    public interface IAgentChatRepository
    {
        Task<long> SaveMessageAsync(AgentChatMessageModel model);
        Task<List<AgentChatMessageDto>> GetPageNewestFirstAsync(Guid sessionId, int userId, long? beforeId, int limit);
    }
}
