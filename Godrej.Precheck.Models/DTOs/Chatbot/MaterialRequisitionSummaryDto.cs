namespace Godrej.Precheck.Models.DTOs.Chatbot
{
    public class MaterialRequisitionSummaryDto
    {
        public string? RequestNumber { get; set; }
        public string? Status { get; set; }
        public string? ProductionOrderNumber { get; set; }
        public string? DrawingNumber { get; set; }
        public string? LnItemCode { get; set; }
        public float? Quantity { get; set; }
        public string? Unit { get; set; }
        public bool? IsRejected { get; set; }
        public string? RequestOwner { get; set; }
        public DateTime? CreatedDate { get; set; }
    }
}
