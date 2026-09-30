using Godrej.Precheck.Models.DataModel.Chatbot;
using Godrej.Precheck.Repository.Repository.ChatbotRepository;
using Microsoft.Extensions.Logging;

namespace Godrej.Precheck.Service.Service.ChatbotService
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

        public Task<int> CreateSessionAsync(int userId, string? title)
            => _chatbotRepository.CreateSessionAsync(userId, title);

        public Task<bool> SessionBelongsToUserAsync(int sessionId, int userId)
            => _chatbotRepository.SessionBelongsToUserAsync(sessionId, userId);

        public Task<string?> GetSessionStateAsync(int sessionId)
            => _chatbotRepository.GetSessionStateAsync(sessionId);

        public Task UpdateSessionStateAsync(int sessionId, string sessionState)
            => _chatbotRepository.UpdateSessionStateAsync(sessionId, sessionState);

        public Task AddMessageAsync(int sessionId, string role, string content, string? toolCalled)
            => _chatbotRepository.AddMessageAsync(sessionId, role, content, toolCalled);

        public Task<List<ChatMessageRecord>> GetMessagesAsync(int sessionId)
            => _chatbotRepository.GetMessagesAsync(sessionId);
    }
}
