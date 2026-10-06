namespace Precheck.Models.DTOs.Chatbot
{
    // Trimmed projection of PendingPrecheckResponseDto - the full type inherits every
    // ProductionOrderMasterDto field plus a nested PendingIdNumbers->Childs tree, which is far
    // too large to feed back into the model. Only the pending unit count is kept, not the detail.
    public class PendingPrecheckSummaryDto
    {
        public string? ProductionOrderNumber { get; set; }
        public string? DrawingNumber { get; set; }
        public string? LnItemCode { get; set; }
        public int PendingUnitCount { get; set; }
    }
}
