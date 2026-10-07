namespace Precheck.Models.DataModel.Chatbot
{
    public class AgentChatMessageModel
    {
        public long Id { get; set; }
        public Guid SessionId { get; set; }
        public string Request { get; set; } = string.Empty;
        public string? Response { get; set; }
        public string? ToolTrace { get; set; }
        public int? InputTokens { get; set; }
        public int? OutputTokens { get; set; }
        public int CreatedBy { get; set; }
    }
}
