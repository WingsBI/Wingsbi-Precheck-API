namespace Precheck.Models.DTOs.Chatbot
{
    public class ChatRequestDto
    {
        public int? SessionId { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
