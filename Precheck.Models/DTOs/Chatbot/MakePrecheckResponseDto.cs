namespace Precheck.Models.DTOs.Chatbot
{
    public class MakePrecheckResponseDto
    {
        public bool Created { get; set; }
        public string? ProductionOrderNumber { get; set; }
        public string? ProductionSeries { get; set; }
        public int? AssemblyIdNumber { get; set; }
        public string? AssemblyDrawingNumber { get; set; }
        public string? ComponentDrawingNumber { get; set; }
        public string? QrCodeNumber { get; set; }
        public string? ComponentType { get; set; }
        public decimal? UpdatedQuantity { get; set; }
        public string? Remarks { get; set; }
        public string? PrecheckStatus { get; set; }
        public bool? IsPrecheckComplete { get; set; }
        public decimal? RemainingQuantity { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
