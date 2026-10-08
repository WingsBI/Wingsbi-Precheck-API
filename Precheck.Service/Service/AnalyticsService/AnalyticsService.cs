using Precheck.Models.DTOs.Analytics;
using Precheck.Repository.Repository.AnalyticsRepository;
using Microsoft.Extensions.Logging;

namespace Precheck.Service.Service.AnalyticsService
{
    public class AnalyticsService : IAnalyticsService
    {
        private readonly IAnalyticsRepository _analyticsRepository;
        private readonly ILogger<AnalyticsService> _logger;

        public AnalyticsService(IAnalyticsRepository analyticsRepository, ILogger<AnalyticsService> logger)
        {
            _analyticsRepository = analyticsRepository;
            _logger = logger;
        }

        public async Task<AnalyticsSummaryResponse> GetSummaryAsync()
        {
            _logger.LogInformation("AnalyticsService: Getting dashboard summary");
            return await _analyticsRepository.GetSummaryAsync();
        }
    }
}
