using Precheck.Models.DTOs.Analytics;

namespace Precheck.Repository.Repository.AnalyticsRepository
{
    public interface IAnalyticsRepository
    {
        Task<AnalyticsSummaryResponse> GetSummaryAsync();
    }
}
