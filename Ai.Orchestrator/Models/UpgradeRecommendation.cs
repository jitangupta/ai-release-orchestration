namespace Ai.Orchestrator.Models;

public class UpgradeRecommendation
{
    public string TenantId { get; set; } = string.Empty;
    public string ReleaseVersion { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty; // MUST, SHOULD, SKIP
    public string Reasoning { get; set; } = string.Empty;
    public List<string> AffectedFeatures { get; set; } = new();
    public string EstimatedImpact { get; set; } = string.Empty; // high, medium, low
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}