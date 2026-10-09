using Precheck.Models.DataModel.Chatbot;
using Precheck.Models.DTOs.Chatbot;
using Precheck.Repository.Database;
using Precheck.Repository.Queries;
using Microsoft.Extensions.Logging;

namespace Precheck.Repository.Repository.AgentChatRepository
{
    public class AgentChatRepository : IAgentChatRepository
    {
        private readonly ILogger<AgentChatRepository> _logger;
        private readonly IApplicationDbContext _db;

        public AgentChatRepository(ILogger<AgentChatRepository> logger, IApplicationDbContext db)
        {
            _logger = logger;
            _db = db;
        }

        public async Task<long> SaveMessageAsync(AgentChatMessageModel model)
        {
            try
            {
                return await _db.ExecuteScalar<long>(AgentChatQueries.INSERT_MESSAGE, model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving agent chat message for SessionId: {SessionId}", model.SessionId);
                throw;
            }
        }

        public async Task<List<AgentChatMessageDto>> GetPageNewestFirstAsync(Guid sessionId, int userId, long? beforeId, int limit)
        {
            try
            {
                var results = await _db.GetAll<AgentChatMessageDto>(
                    AgentChatQueries.SELECT_SESSION_PAGE,
                    new { SessionId = sessionId, UserId = userId, BeforeId = beforeId, Limit = limit });

                return results.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching agent chat page for SessionId: {SessionId}", sessionId);
                throw;
            }
        }
    }
}
