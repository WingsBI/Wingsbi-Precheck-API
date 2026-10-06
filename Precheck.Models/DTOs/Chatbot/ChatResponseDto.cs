namespace Precheck.Models.DTOs.Chatbot
{
    public class ChatResponseDto
    {
        public int SessionId { get; set; }
        public string Answer { get; set; } = string.Empty;
        public string? ToolCalled { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public object? Data { get; set; }
        public List<string> SuggestedQuestions { get; set; } = new();
        public long? InputTokens { get; set; }
        public long? OutputTokens { get; set; }
        public long? TotalTokens { get; set; }
    }
}
