namespace Godrej.Precheck.Models.DTOs.Chatbot
{
    public class ChatResponseDto
    {
        public int SessionId { get; set; }
        public string Answer { get; set; } = string.Empty;
        public string? ToolCalled { get; set; }
        public object? Data { get; set; }
        public List<string> SuggestedQuestions { get; set; } = new();
    }
}
