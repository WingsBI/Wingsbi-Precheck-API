using Precheck.Models.DTOs.QRCodeDetails;

namespace Precheck.Models.DTOs.Chatbot
{
    // The ID numbers a Production Order actually covers, and which of them already have a QR code. Lets the chatbot
    // offer real ID numbers when a user wants to generate QR codes, instead of made-up examples.
    public class ProductionOrderIdNumbersDto
    {
        public string? ProductionOrderNumber { get; set; }
        public string? ProductionSeries { get; set; }
        public string? DrawingNumber { get; set; }
        // The drawing's mapped component type (ID / FIM / SI / BATCH). Informational: it does not stop a user generating
        // 'ID' QR codes for this Production Order.
        public string? ComponentType { get; set; }
        public int? StartIdNumber { get; set; }
        public int? EndIdNumber { get; set; }
        public int TotalIdNumbers { get; set; }
        // Compact ranges, e.g. "24" or "24, 26-28".
        public string IdNumbersWithQrCode { get; set; } = string.Empty;
        public string IdNumbersWithoutQrCode { get; set; } = string.Empty;
        public int CountWithoutQrCode { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class QrCodeStatusSummaryDto
    {
        public int TotalQrCodes { get; set; }
        public List<QrCodeStatusCountDto> ByStatus { get; set; } = new();
        public string Message { get; set; } = string.Empty;
    }
}
