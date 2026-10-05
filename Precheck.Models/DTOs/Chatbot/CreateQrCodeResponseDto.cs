namespace Precheck.Models.DTOs.Chatbot
{
    public class CreateQrCodeResponseDto
    {
        public bool Created { get; set; }
        public string? ProductionOrderNumber { get; set; }
        public string? PartNumber { get; set; }
        public string? ItemCode { get; set; }
        public string? ProductionSeries { get; set; }
        public string? ComponentType { get; set; }
        public string? IdNumbers { get; set; }
        public decimal? Quantity { get; set; }
        public string? BuildNumber { get; set; }
        public string? OperationNumber { get; set; }
        public string? Remark { get; set; }
        public List<string> GeneratedQrCodeNumbers { get; set; } = new();
        public List<string> AlreadyExistingQrCodeNumbers { get; set; } = new();
        public string Message { get; set; } = string.Empty;
    }
}
