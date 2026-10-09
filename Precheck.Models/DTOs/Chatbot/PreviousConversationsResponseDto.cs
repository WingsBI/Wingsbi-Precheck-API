namespace Precheck.Models.DTOs.Chatbot
{
    public class ChatSessionItemDto
    {
        public int Id { get; set; }
        public string Request { get; set; } = string.Empty;
        public string Response { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
    }

    public class GetSessionRequestDto
    {
        // A Guid (AG-UI / Copilot sessions) or a numeric id (legacy int sessions), sent as a string.
        public string SessionId { get; set; } = string.Empty;
    }

    public class SessionMessagesResponseDto
    {
        // The session these messages belong to. For a Copilot (Guid) session it is the same id that was
        // requested, so the frontend keeps using it to continue the chat.
        public string SessionId { get; set; } = string.Empty;
        // Legacy (numeric) sessions only: fresh session id for the frontend to store; null for Guid sessions.
        public int? NewSessionId { get; set; }
        // Oldest first.
        public List<ChatSessionItemDto> Messages { get; set; } = new();
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
