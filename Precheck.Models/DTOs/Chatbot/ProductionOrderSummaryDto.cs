namespace Precheck.Models.DTOs.Chatbot
{
    // Slim projection of ProductionOrderMasterDto for chatbot tool results - keeps only the
    // fields useful for answering a chat question. Audit/UI-only columns (createdBy, modifiedBy,
    // modifiedDate, etc.) are dropped to control token usage when a broad query matches many rows.
    public class ProductionOrderSummaryDto
    {
        public string ProductionOrderNumber { get; set; } = string.Empty;
        public string? Status { get; set; }
        public string? LnItemCode { get; set; }
        public string? DrawingNumber { get; set; }
        public int? Quantity { get; set; }
        public DateTime? CreatedDate { get; set; } 
    }
}

