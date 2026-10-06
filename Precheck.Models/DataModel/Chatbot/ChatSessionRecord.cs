namespace Precheck.Models.DataModel.Chatbot
{
    public class ChatSessionRecord
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int SessionId { get; set; }
        public string Request { get; set; } = string.Empty;
        public string Response { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
    }
}
