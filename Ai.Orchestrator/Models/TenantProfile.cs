namespace Ai.Orchestrator.Models;

public class TenantProfile
{
    public string TenantId { get; set; } = string.Empty;
    public List<string> ActiveFeatures { get; set; } = new();
    public string UsagePattern { get; set; } = "medium"; // high, medium, low
    public string RiskTolerance { get; set; } = "medium"; // high, medium, low
    public DateTime? LastUpgradeDate { get; set; }
}