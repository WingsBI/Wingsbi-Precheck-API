namespace Godrej.Precheck.Models.DTOs.Chatbot
{
    public class IRNumberSummaryDto
    {
        public string? IrNumber { get; set; }
        public string? ProductionOrderNumber { get; set; }
        public string? DrawingNumberIdName { get; set; }
        public string? Stage { get; set; }
        public int? Quantity { get; set; }
        public DateTime? CreatedDate { get; set; }
    }
}
