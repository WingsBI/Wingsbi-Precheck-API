namespace Precheck.Models.DTOs.Analytics
{
    public class AnalyticsSummaryResponse
    {
        public ProductionOrderCounts ProductionOrders { get; set; } = new();
        public QrCodeCounts QrCodes { get; set; } = new();
        public MaterialRequisitionCounts MaterialRequisition { get; set; } = new();
        public ComponentSwapCounts ComponentSwap { get; set; } = new();
    }

    public class ProductionOrderCounts
    {
        public int Pending { get; set; }
        public int Partial { get; set; }
        public int Completed { get; set; }
    }

    public class QrCodeCounts
    {
        public int ReadyForConsumption { get; set; }
        public int Generated { get; set; }
        public int Consumed { get; set; }
    }

    public class MaterialRequisitionCounts
    {
        public int RejectedComponents { get; set; }
    }

    public class ComponentSwapCounts
    {
        public int PendingSwap { get; set; }
    }

    /// <summary>Flat row returned by the summary query: one (Metric, Count) pair per bucket.</summary>
    public class AnalyticsMetricRow
    {
        public string Metric { get; set; } = string.Empty;
        public int Total { get; set; }
    }
}
