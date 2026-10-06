namespace Precheck.Models.DTOs.Chatbot
{
    public class CreateIrMsnResponseDto
    {
        public bool Created { get; set; }
        public string? GeneratedNumber { get; set; }
        public string? ProductionOrderNumber { get; set; }
        public string? PurchaseOrderNumber { get; set; }
        public string? PartNumber { get; set; }
        public string? ItemCode { get; set; }
        public string? ProductionSeries { get; set; }
        public string? ProjectNumber { get; set; }
        public string IdNumbers { get; set; } = string.Empty;
        public int? Quantity { get; set; }
        public string? BuildNumber { get; set; }
        public string? Stage { get; set; }
        public string? OperationNumber { get; set; }
        public string? Remark { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
