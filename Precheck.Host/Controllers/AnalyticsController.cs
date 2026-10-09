using Precheck.Models.DTOs.Analytics;
using Precheck.Service.Service.AnalyticsService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Precheck.Host.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AnalyticsController : ControllerBase
    {
        private readonly IAnalyticsService _analyticsService;
        private readonly ILogger<AnalyticsController> _logger;

        public AnalyticsController(IAnalyticsService analyticsService, ILogger<AnalyticsController> logger)
        {
            _analyticsService = analyticsService;
            _logger = logger;
        }

        [Authorize]
        [HttpGet("summary")]
        [ProducesResponseType(typeof(AnalyticsSummaryResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetSummary()
        {
            _logger.LogInformation("Request received for AnalyticsController:GetSummary");

            try
            {
                var result = await _analyticsService.GetSummaryAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AnalyticsController:GetSummary - Unexpected error occurred");
                return StatusCode(500, new { message = "An unexpected error occurred. Please try again later." });
            }
        }
    }
}
