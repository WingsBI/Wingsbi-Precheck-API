namespace Precheck.Models.DTOs.Chatbot
{
    public class PrecheckRecordSummaryDto
    {
        public string? ProductionOrderNumber { get; set; }
        public string? ProductionSeries { get; set; }
        public string? DrawingNumber { get; set; }
        public string? IdNumber { get; set; }
        public string? QrCodeNumber { get; set; }
        public string? LnItemCode { get; set; }
        public string? ComponentType { get; set; }
        public decimal? Quantity { get; set; }
        public decimal? RemainingQuantity { get; set; }
        public string? Unit { get; set; }
        public string? PrecheckStatus { get; set; }
        public bool IsPrecheckComplete { get; set; }
        public bool? IsRejected { get; set; }
        public string? Remarks { get; set; }
        public DateTime? PrecheckDate { get; set; }
        public string? IrNumber { get; set; }
        public string? MsnNumber { get; set; }
    }
}
