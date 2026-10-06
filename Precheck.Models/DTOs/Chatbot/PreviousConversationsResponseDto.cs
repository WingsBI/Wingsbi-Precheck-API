namespace Precheck.Models.DTOs.Chatbot
{
    public class ChatSessionItemDto
    {
        public int Id { get; set; }
        public string Request { get; set; } = string.Empty;
        public string Response { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
    }

    public class PreviousConversationsResponseDto
    {
        // Oldest first, so the client can render them in chronological order.
        public List<ChatSessionItemDto> Messages { get; set; } = new();
        // Pass back as the cursor to load the next (older) page; null when there is nothing older.
        public int? NextCursor { get; set; }
        public bool HasMore { get; set; }
    }
}
