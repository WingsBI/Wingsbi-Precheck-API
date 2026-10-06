namespace Precheck.Models.DTOs.Chatbot
{
    public class SwappingDetailSummaryDto
    {
        public string? SwappedDrawingNumber { get; set; }
        public string? FromSwappedIdNumber { get; set; }
        public string? ToSwappedIdNumber { get; set; }
        public string? SwappedFromPONumber { get; set; }
        public string? SwappedToPONumber { get; set; }
        public DateTime? CreatedDate { get; set; }
    }
}
