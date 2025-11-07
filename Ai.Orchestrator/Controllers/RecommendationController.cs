using Ai.Orchestrator.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ai.Orchestrator.Controllers;

[ApiController]
[Route("api/recommendations")]
public class RecommendationController : ControllerBase
{
    private readonly RecommendationEngine _engine;
    private readonly ILogger<RecommendationController> _logger;

    public RecommendationController(RecommendationEngine engine, ILogger<RecommendationController> logger)
    {
        _engine = engine;
        _logger = logger;
    }

    [HttpPost("generate")]
    public async Task<IActionResult> GenerateRecommendations([FromBody] GenerateRequest request)
    {
        _logger.LogInformation("API Request: Generate recommendations for release {ReleaseVersion}, {TenantCount} tenants: {TenantIds}",
            request.ReleaseVersion, request.TenantIds.Count, string.Join(", ", request.TenantIds));

        try
        {
            var recommendations = await _engine.GenerateRecommendationsAsync(
                request.ReleaseVersion,
                request.TenantIds);

            _logger.LogInformation("Successfully generated {Count} recommendations for release {ReleaseVersion}",
                recommendations.Count, request.ReleaseVersion);

            return Ok(new
            {
                success = true,
                count = recommendations.Count,
                recommendations
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate recommendations for release {ReleaseVersion}", request.ReleaseVersion);
            return StatusCode(500, new
            {
                success = false,
                error = ex.Message
            });
        }
    }
}

public record GenerateRequest(string ReleaseVersion, List<string> TenantIds);