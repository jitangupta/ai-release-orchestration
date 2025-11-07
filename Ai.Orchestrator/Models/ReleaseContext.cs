namespace Ai.Orchestrator.Models;

public class ReleaseContext
{
    public string ReleaseVersion { get; set; } = string.Empty;
    public List<JiraTicket> RelevantTickets { get; set; } = new();
    public ReleaseMetadata? Metadata { get; set; }
}

public class ReleaseMetadata
{
    public string ReleaseVersion { get; set; } = string.Empty;
    public string ReleaseDate { get; set; } = string.Empty;
    public string DeploymentComplexity { get; set; } = string.Empty;
    public List<string> RequiredPredecessorVersions { get; set; } = new();
    public string ReleaseSummary { get; set; } = string.Empty;
    public List<ReleaseChange> ContentBreakdown { get; set; } = new();
}

public class ReleaseChange
{
    public string ChangeId { get; set; } = string.Empty;
    public string ChangeType { get; set; } = string.Empty;
    public string LinkedFeatureId { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string DeploymentImpact { get; set; } = string.Empty;
}

public class JiraTicket
{
    public string TicketId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> AffectedFeatures { get; set; } = new();
}