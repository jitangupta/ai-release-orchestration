using Ai.Orchestrator.Models;
using System.Text.Json;

namespace Ai.Orchestrator.Services;

public class RecommendationEngine
{
    private readonly QdrantRetrieverService _retriever;
    private readonly LLMReasoningService _llmService;
    private readonly string _outputDirectory;
    private readonly ILogger<RecommendationEngine> _logger;

    public RecommendationEngine(
        QdrantRetrieverService retriever,
        LLMReasoningService llmService,
        IConfiguration configuration,
        ILogger<RecommendationEngine> logger)
    {
        _retriever = retriever;
        _llmService = llmService;
        _logger = logger;
        _outputDirectory = configuration["OutputDirectory"] ?? "Data/recommendations";
        Directory.CreateDirectory(_outputDirectory);

        _logger.LogInformation("RecommendationEngine initialized. Output directory: {OutputDirectory}", _outputDirectory);
    }

    public async Task<List<UpgradeRecommendation>> GenerateRecommendationsAsync(
        string releaseVersion,
        List<string> tenantIds)
    {
        _logger.LogInformation("Starting recommendation generation for release {ReleaseVersion}, {TenantCount} tenants",
            releaseVersion, tenantIds.Count);

        var recommendations = new List<UpgradeRecommendation>();

        // Load release metadata
        _logger.LogInformation("Loading release metadata for {ReleaseVersion}", releaseVersion);
        var releaseMetadata = LoadReleaseMetadata(releaseVersion);
        _logger.LogInformation("Release metadata loaded. Deployment complexity: {Complexity}, Total changes: {ChangeCount}",
            releaseMetadata.DeploymentComplexity, releaseMetadata.ContentBreakdown.Count);

        // Load tenant profiles
        _logger.LogInformation("Loading tenant profiles for {TenantIdList}", string.Join(", ", tenantIds));
        var tenants = LoadTenantProfiles(tenantIds);
        _logger.LogInformation("Loaded {TenantCount} tenant profiles", tenants.Count);

        foreach (var tenant in tenants)
        {
            _logger.LogInformation("Processing tenant {TenantId} ({CurrentTenant}/{TotalTenants})",
                tenant.TenantId, recommendations.Count + 1, tenants.Count);

            // 1. Retrieve relevant tickets from Qdrant using hybrid approach
            _logger.LogInformation("Retrieving relevant tickets for tenant {TenantId}", tenant.TenantId);
            var tickets = await _retriever.GetRelevantTicketsAsync(releaseVersion, tenant.ActiveFeatures, tenant);
            _logger.LogInformation("Retrieved {TicketCount} relevant tickets for tenant {TenantId}", tickets.Count, tenant.TenantId);

            // 2. Build release context with metadata
            var releaseContext = new ReleaseContext
            {
                ReleaseVersion = releaseVersion,
                RelevantTickets = tickets,
                Metadata = releaseMetadata
            };

            // 3. Generate recommendation using LLM
            _logger.LogInformation("Generating LLM recommendation for tenant {TenantId}", tenant.TenantId);
            var recommendation = await _llmService.GenerateRecommendationAsync(tenant, releaseContext);
            _logger.LogInformation("Recommendation for tenant {TenantId}: {Recommendation}, Impact: {Impact}",
                tenant.TenantId, recommendation.Recommendation, recommendation.EstimatedImpact);
            recommendations.Add(recommendation);

            // 4. Save to file
            await SaveRecommendationAsync(recommendation);
        }

        _logger.LogInformation("Completed recommendation generation. Total: {Total}, MUST: {Must}, SHOULD: {Should}, SKIP: {Skip}",
            recommendations.Count,
            recommendations.Count(r => r.Recommendation == "MUST"),
            recommendations.Count(r => r.Recommendation == "SHOULD"),
            recommendations.Count(r => r.Recommendation == "SKIP"));

        return recommendations;
    }

    private ReleaseMetadata LoadReleaseMetadata(string releaseVersion)
    {
        var filePath = $"Data/{releaseVersion}.json";
        var json = File.ReadAllText(filePath);
        var jsonDoc = JsonSerializer.Deserialize<JsonDocument>(json);

        if (jsonDoc == null)
            throw new InvalidOperationException($"Failed to load release metadata for {releaseVersion}");

        var root = jsonDoc.RootElement;

        var metadata = new ReleaseMetadata
        {
            ReleaseVersion = root.GetProperty("release_version").GetString() ?? "",
            ReleaseDate = root.GetProperty("release_date").GetString() ?? "",
            DeploymentComplexity = root.GetProperty("deployment_complexity").GetString() ?? "",
            RequiredPredecessorVersions = root.GetProperty("required_predecessor_versions").EnumerateArray()
                .Select(v => v.GetString() ?? "").ToList(),
            ReleaseSummary = root.GetProperty("release_summary").GetString() ?? "",
            ContentBreakdown = root.GetProperty("content_breakdown").EnumerateArray()
                .Select(item => new ReleaseChange
                {
                    ChangeId = item.GetProperty("change_id").GetString() ?? "",
                    ChangeType = item.GetProperty("change_type").GetString() ?? "",
                    LinkedFeatureId = item.GetProperty("linked_feature_id").GetString() ?? "",
                    Severity = item.GetProperty("severity").GetString() ?? "",
                    DeploymentImpact = item.GetProperty("deployment_impact").GetString() ?? ""
                }).ToList()
        };

        return metadata;
    }

    private List<TenantProfile> LoadTenantProfiles(List<string> tenantIds)
    {
        var filePath = "Data/tenants.jsonl";
        var allTenants = new List<TenantProfile>();

        // Read JSONL file (each line is a separate JSON object)
        foreach (var line in File.ReadAllLines(filePath))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var jsonDoc = JsonSerializer.Deserialize<JsonDocument>(line);
            if (jsonDoc == null) continue;

            var root = jsonDoc.RootElement;

            var tenant = new TenantProfile
            {
                TenantId = root.GetProperty("tenant_id").GetString() ?? "",
                ActiveFeatures = root.GetProperty("active_features").EnumerateArray()
                    .Select(f => f.GetString() ?? "").ToList(),
                UsagePattern = root.GetProperty("daily_usage_score").GetInt32() > 70 ? "high" : "medium",
                RiskTolerance = root.GetProperty("risk_tolerance").GetString()?.ToLower() ?? "medium"
            };

            allTenants.Add(tenant);
        }

        return allTenants.Where(t => tenantIds.Contains(t.TenantId)).ToList();
    }

    private async Task SaveRecommendationAsync(UpgradeRecommendation recommendation)
    {
        var fileName = $"{recommendation.TenantId}_{recommendation.ReleaseVersion}_{DateTime.UtcNow:yyyyMMddHHmmss}.json";
        var filePath = Path.Combine(_outputDirectory, fileName);

        _logger.LogDebug("Saving recommendation to file: {FilePath}", filePath);

        var json = JsonSerializer.Serialize(recommendation, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath, json);

        _logger.LogInformation("Saved recommendation for tenant {TenantId} to {FilePath}", recommendation.TenantId, filePath);
    }
}