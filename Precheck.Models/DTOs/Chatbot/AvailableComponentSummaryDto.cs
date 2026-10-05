namespace Precheck.Models.DTOs.Chatbot
{
    public class AvailableComponentSummaryDto
    {
        public string? DrawingNumber { get; set; }
        public string? LnItemCode { get; set; }
        public string? Location { get; set; }
        public string? QrCodeNumber { get; set; }
        public string? IdNumber { get; set; }
        public decimal Quantity { get; set; }
        public decimal RemainingQuantity { get; set; }
        public string? ProductionOrderNumber { get; set; }
        public string? Status { get; set; }
    }
}
