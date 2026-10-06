namespace Precheck.Models.DTOs.Chatbot
{
    public class QrCodeSummaryDto
    {
        public string? QrCodeNumber { get; set; }
        public string? QrCodeStatus { get; set; }
        public string? ProductionOrderNumber { get; set; }
        public string? RackLocation { get; set; }
        public string? IdNumber { get; set; }
        public DateTime? ExpiryDate { get; set; }
    }
}
