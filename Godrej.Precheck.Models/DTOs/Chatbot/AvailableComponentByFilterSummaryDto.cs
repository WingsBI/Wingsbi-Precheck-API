namespace Godrej.Precheck.Models.DTOs.Chatbot
{
    public class AvailableComponentByFilterSummaryDto
    {
        public string? IdNumber { get; set; }
        public decimal Quantity { get; set; }
        public string? DrawingNumber { get; set; }
        public string? ProductionSeries { get; set; }
        public string? Nomenclature { get; set; }
        public string? ProductionOrderNumber { get; set; }
        public string? PrecheckStatus { get; set; }
    }
}
