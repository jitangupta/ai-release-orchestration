using Ai.Orchestrator.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ai.Orchestrator.Controllers;

[ApiController]
[Route("api/recommendations")]
public class RecommendationController : ControllerBase
{
    private readonly RecommendationEngine _engine;

    public RecommendationController(RecommendationEngine engine)
    {
        _engine = engine;
    }

    [HttpPost("generate")]
    public async Task<IActionResult> GenerateRecommendations([FromBody] GenerateRequest request)
    {
        var recommendations = await _engine.GenerateRecommendationsAsync(
            request.ReleaseVersion,
            request.TenantIds);

        return Ok(new
        {
            success = true,
            count = recommendations.Count,
            recommendations
        });
    }
}

public record GenerateRequest(string ReleaseVersion, List<string> TenantIds);