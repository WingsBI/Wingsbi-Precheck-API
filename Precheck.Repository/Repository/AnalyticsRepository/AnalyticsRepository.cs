using Precheck.Models.DTOs.Analytics;
using Precheck.Repository.Database;
using Precheck.Repository.Queries;
using Microsoft.Extensions.Logging;

namespace Precheck.Repository.Repository.AnalyticsRepository
{
    public class AnalyticsRepository : IAnalyticsRepository
    {
        private readonly ILogger<AnalyticsRepository> _logger;
        private readonly IApplicationDbContext _db;

        public AnalyticsRepository(ILogger<AnalyticsRepository> logger, IApplicationDbContext db)
        {
            _logger = logger;
            _db = db;
        }

        public async Task<AnalyticsSummaryResponse> GetSummaryAsync()
        {
            try
            {
                _logger.LogInformation("Starting GetSummaryAsync");
                var rows = await _db.GetAll<AnalyticsMetricRow>(AnalyticsQueries.GET_ANALYTICS_SUMMARY, new { });
                var counts = rows.ToDictionary(r => r.Metric, r => r.Total);

                int Get(string key) => counts.TryGetValue(key, out var v) ? v : 0;

                return new AnalyticsSummaryResponse
                {
                    ProductionOrders = new ProductionOrderCounts
                    {
                        Pending = Get("po_pending"),
                        Partial = Get("po_partial"),
                        Completed = Get("po_completed")
                    },
                    QrCodes = new QrCodeCounts
                    {
                        ReadyForConsumption = Get("qr_ready"),
                        Generated = Get("qr_generated"),
                        Consumed = Get("qr_consumed")
                    },
                    MaterialRequisition = new MaterialRequisitionCounts { RejectedComponents = Get("mr_rejected") },
                    ComponentSwap = new ComponentSwapCounts { PendingSwap = Get("swap_pending") }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve analytics summary. Error: {ErrorMessage}", ex.Message);
                throw;
            }
        }
    }
}
