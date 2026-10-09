namespace Precheck.Models.DTOs.Chatbot
{
    public class AgentChatMessageDto
    {
        public long Id { get; set; }
        public string Request { get; set; } = string.Empty;
        public string? Response { get; set; }
        public int? InputTokens { get; set; }
        public int? OutputTokens { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    // One page of a session's turns, oldest first. NextCursor is the id to pass as beforeId for the next older page.
    public class AgentChatPageDto
    {
        public List<AgentChatMessageDto> Items { get; set; } = new();
        public long? NextCursor { get; set; }
        public bool HasMore { get; set; }
    }
}
