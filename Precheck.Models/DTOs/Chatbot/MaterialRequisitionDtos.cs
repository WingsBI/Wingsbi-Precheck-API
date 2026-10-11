namespace Precheck.Models.DTOs.Chatbot
{
    // One row of a "count per status" result.
    public class StatusCountDto
    {
        public string Status { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class MaterialRequisitionStatusSummaryDto
    {
        public int Total { get; set; }
        public List<StatusCountDto> ByStatus { get; set; } = new();
        public string Message { get; set; } = string.Empty;
    }

    // Everything the requisition screen shows for ONE request: the rejected component and why, its IR/MSN/MRIR
    // numbers, who raised it and when.
    public class MaterialRequisitionDetailDto
    {
        public string? RequestNumber { get; set; }
        public string? Status { get; set; }
        public string? ProductionOrderNumber { get; set; }
        public string? ProjectNumber { get; set; }
        public string? ProductionSeries { get; set; }
        public string? ComponentDrawingNumber { get; set; }
        public string? Nomenclature { get; set; }
        public string? LnItemCode { get; set; }
        public string? ComponentType { get; set; }
        public string? IdNumber { get; set; }
        public string? IrNumber { get; set; }
        public string? MsnNumber { get; set; }
        public string? MrirNumber { get; set; }
        public string? ConsumedInDrawing { get; set; }
        public float? Quantity { get; set; }
        public string? Unit { get; set; }
        public string? Remarks { get; set; }
        public bool? IsRejected { get; set; }
        public bool? IsPrecheckComplete { get; set; }
        public string? Hwno { get; set; }
        public string? RequestOwner { get; set; }
        public string? RaisedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public DateTime? PrecheckDate { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
