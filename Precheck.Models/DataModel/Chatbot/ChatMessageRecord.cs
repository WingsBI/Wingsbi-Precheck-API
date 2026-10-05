namespace Precheck.Models.DataModel.Chatbot
{
    public class ChatMessageRecord
    {
        public int Id { get; set; }
        public int SessionId { get; set; }
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string? ToolCalled { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
