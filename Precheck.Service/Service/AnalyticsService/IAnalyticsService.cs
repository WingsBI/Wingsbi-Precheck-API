using Precheck.Models.DTOs.Analytics;

namespace Precheck.Service.Service.AnalyticsService
{
    public interface IAnalyticsService
    {
        Task<AnalyticsSummaryResponse> GetSummaryAsync();
    }
}
