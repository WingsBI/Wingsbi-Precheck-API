namespace Precheck.Models.DTOs.Chatbot
{
    public class AvailableQrGroupSummaryDto
    {
        public string? DrawingNumber { get; set; }
        public string? LnItemCode { get; set; }
        public string? ProductionSeries { get; set; }
        public string? ComponentType { get; set; }
        public decimal TotalQuantity { get; set; }
        public decimal TotalRemainingQuantity { get; set; }
        public int QrCount { get; set; }
        public List<string> QrCodeNumbers { get; set; } = new();
    }
}
