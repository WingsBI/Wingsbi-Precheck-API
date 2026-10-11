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

    // Where a QR code can be consumed: the Production Orders (and their ID numbers) whose precheck still needs this
    // QR code's component.
    public class QrConsumptionOptionsDto
    {
        public string? QrCodeNumber { get; set; }
        public string? QrCodeStatus { get; set; }
        public string? DrawingNumber { get; set; }
        public string? ProductionSeries { get; set; }
        public string? ComponentType { get; set; }
        public decimal? RemainingQuantity { get; set; }
        public bool CanBeConsumed { get; set; }
        public int ProductionOrderCount { get; set; }
        // The assembly drawing(s) this component is part of: a Production Order for one of them is where it gets consumed.
        public List<string> ParentAssemblies { get; set; } = new();
        public List<QrConsumptionTargetDto> ConsumableIn { get; set; } = new();
        public string Message { get; set; } = string.Empty;
    }

    public class QrConsumptionTargetDto
    {
        public string? ProductionOrderNumber { get; set; }
        // Compact ranges of the ID numbers in this Production Order that still need the component, e.g. "24-26, 28".
        public string IdNumbers { get; set; } = string.Empty;
        public int PendingLines { get; set; }
        public decimal RequiredQuantity { get; set; }
        public string? PrecheckStatus { get; set; }
    }

    public class QrCodeStatusSummaryDto
    {
        public int TotalQrCodes { get; set; }
        public List<QrCodeStatusCountDto> ByStatus { get; set; } = new();
        public string Message { get; set; } = string.Empty;
    }
}
