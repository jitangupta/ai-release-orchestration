using Ai.Orchestrator.Models;
using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;
using System.Text.Json;

namespace Ai.Orchestrator.Services;

public class LLMReasoningService
{
    private readonly ChatClient _chatClient;
    private readonly string _outputDirectory;
    private readonly ILogger<LLMReasoningService> _logger;

    public LLMReasoningService(IConfiguration configuration, ILogger<LLMReasoningService> logger)
    {
        _logger = logger;

        var endpoint = configuration["AzureOpenAI:Endpoint"] ?? throw new ArgumentNullException("AzureOpenAI:Endpoint");
        var apiKey = configuration["AzureOpenAI:ApiKey"] ?? throw new ArgumentNullException("AzureOpenAI:ApiKey");
        var deploymentName = configuration["AzureOpenAI:ChatDeploymentName"] ?? throw new ArgumentNullException("AzureOpenAI:ChatDeploymentName");

        var client = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
        _chatClient = client.GetChatClient(deploymentName);
        _outputDirectory = configuration["PromptDirectory"] ?? "Data/generatedprompts";

        _logger.LogInformation("LLMReasoningService initialized. Chat deployment: {Deployment}, Prompt directory: {PromptDirectory}",
            deploymentName, _outputDirectory);
    }

    public async Task<UpgradeRecommendation> GenerateRecommendationAsync(
        TenantProfile tenant,
        ReleaseContext releaseContext)
    {
        _logger.LogInformation("Generating recommendation for tenant {TenantId}, release {ReleaseVersion}",
            tenant.TenantId, releaseContext.ReleaseVersion);

        var prompt = BuildPrompt(tenant, releaseContext);
        _logger.LogDebug("Built prompt for tenant {TenantId}, prompt length: {Length} chars", tenant.TenantId, prompt.Length);

        await SavepromptsAsync(tenant, prompt);

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage("You are an expert system that analyzes software releases and generates upgrade recommendations."),
            new UserChatMessage(prompt)
        };

        _logger.LogInformation("Calling LLM for tenant {TenantId}", tenant.TenantId);
        var response = await _chatClient.CompleteChatAsync(messages);
        var content = response.Value.Content[0].Text;
        _logger.LogDebug("LLM response received for tenant {TenantId}, response length: {Length} chars",
            tenant.TenantId, content.Length);

        // Parse LLM response (expecting JSON)
        var recommendation = ParseRecommendation(content, tenant.TenantId, releaseContext.ReleaseVersion);
        _logger.LogInformation("Parsed recommendation for tenant {TenantId}: {Recommendation}",
            tenant.TenantId, recommendation.Recommendation);

        return recommendation;
    }

    private async Task SavepromptsAsync(TenantProfile tenant, string prompt)
    {
        var fileName = $"{tenant.TenantId}_Prompt_{DateTime.UtcNow:yyyyMMddHHmmss}.txt";
        var filePath = Path.Combine(_outputDirectory, fileName);

        _logger.LogDebug("Saving prompt to file: {FilePath}", filePath);
        await File.WriteAllTextAsync(filePath, prompt);
        _logger.LogInformation("Saved prompt for tenant {TenantId} to {FilePath}", tenant.TenantId, filePath);
    }

    private string BuildPrompt(TenantProfile tenant, ReleaseContext releaseContext)
    {
        _logger.LogDebug("Building prompt for tenant {TenantId}, release {ReleaseVersion}",
            tenant.TenantId, releaseContext.ReleaseVersion);

        var metadata = releaseContext.Metadata;

        // Analyze changes affecting tenant's active features WITH DETAILS
        var criticalChanges = metadata?.ContentBreakdown
            .Where(c => tenant.ActiveFeatures.Contains(c.LinkedFeatureId) && c.Severity == "CRITICAL")
            .ToList() ?? new();

        var majorChanges = metadata?.ContentBreakdown
            .Where(c => tenant.ActiveFeatures.Contains(c.LinkedFeatureId) && c.Severity == "MAJOR")
            .ToList() ?? new();

        var minorChanges = metadata?.ContentBreakdown
            .Where(c => tenant.ActiveFeatures.Contains(c.LinkedFeatureId) && (c.Severity == "MINOR" || c.Severity == "LOW"))
            .ToList() ?? new();

        _logger.LogDebug("Changes affecting tenant {TenantId}: Critical={Critical}, Major={Major}, Minor={Minor}",
            tenant.TenantId, criticalChanges.Count, majorChanges.Count, minorChanges.Count);

        // Get detailed ticket information
        var relevantTicketDetails = string.Join("\n", releaseContext.RelevantTickets.Take(10).Select(t =>
            $"- [{t.Type}] {t.TicketId}: {t.Summary}\n  Features: {string.Join(", ", t.AffectedFeatures)}"));

        if (string.IsNullOrEmpty(relevantTicketDetails))
        {
            relevantTicketDetails = "No relevant tickets found in vector search for this tenant's features.";
        }

        // Build impact analysis
        var impactAnalysis = BuildImpactAnalysis(tenant, criticalChanges, majorChanges, minorChanges);

        return $@"
You are analyzing whether tenant {tenant.TenantId} should upgrade to {releaseContext.ReleaseVersion}.

**IMPORTANT**: Be conservative with MUST recommendations. Only recommend MUST if there are ACTUAL critical bugs affecting this tenant's usage.

**Tenant Profile:**
- Tenant ID: {tenant.TenantId}
- Active Features: {string.Join(", ", tenant.ActiveFeatures)}
- Usage Pattern: {tenant.UsagePattern}
- Risk Tolerance: {tenant.RiskTolerance}

**Release: {releaseContext.ReleaseVersion}**
- Deployment Complexity: {metadata?.DeploymentComplexity}
- Release Summary: {metadata?.ReleaseSummary}
- Total Changes in Release: {metadata?.ContentBreakdown.Count}

**Impact Analysis for This Tenant:**
{impactAnalysis}

**Detailed Relevant Tickets (RAG Results):**
{relevantTicketDetails}

**Enhanced Decision Framework:**

**MUST Upgrade When:**
1. CRITICAL security vulnerabilities in actively used features (e.g., authentication bypass, data leak)
2. CRITICAL data corruption bugs affecting tenant's data (e.g., payment calculation errors for billing-heavy tenants)
3. Production-breaking bugs that tenant is likely experiencing NOW
4. Compliance violations that tenant must fix immediately
5. LOW risk tolerance + CRITICAL bugs in any active feature

**SHOULD Upgrade When:**
1. MAJOR bug fixes in frequently used features
2. Important performance improvements for high-usage tenants
3. MEDIUM risk tolerance + CRITICAL bugs that don't impact current usage
4. Multiple MAJOR improvements that benefit tenant's workflow
5. Security patches that are important but not immediately exploitable
6. CRITICAL bugs in features tenant uses lightly or infrequently

**SKIP Upgrade When:**
1. Only MINOR/LOW severity changes
2. Changes to features tenant doesn't use at all
3. HIGH risk tolerance + only non-critical improvements
4. New features that tenant hasn't requested
5. Performance improvements that don't affect tenant's usage pattern
6. No bugs affecting tenant's specific use case
7. HIGH deployment complexity + minimal tenant benefit

**Risk Tolerance Interpretation:**
- LOW risk tolerance: Upgrade for CRITICAL issues immediately, SHOULD for MAJOR issues
- MEDIUM risk tolerance: Upgrade for CRITICAL + MAJOR issues impacting usage
- HIGH risk tolerance: Only upgrade for severe production-breaking or security issues

**Usage Pattern Interpretation:**
- HIGH usage: More careful - SHOULD for important stability/performance fixes
- MEDIUM usage: Standard evaluation - focus on bugs in used features
- LOW usage: Can SKIP unless CRITICAL security or compliance issue

**Current Guidance:**
- If no tickets match tenant's features: Likely SKIP (unless deployment complexity is LOW and there are nice-to-have improvements)
- If tickets are mostly for unused features: SKIP
- Consider deployment impact: MEDIUM/HIGH complexity requires stronger justification

**Output Format (JSON only):**
{{
  ""recommendation"": ""MUST|SHOULD|SKIP"",
  ""reasoning"": ""Explain the decision based on actual ticket impact, NOT just feature overlap. Be specific about which bugs matter for THIS tenant."",
  ""affectedFeatures"": [""feature1"", ""feature2""],
  ""estimatedImpact"": ""high|medium|low""
}}
";
    }

    private string BuildImpactAnalysis(TenantProfile tenant, List<ReleaseChange> critical, List<ReleaseChange> major, List<ReleaseChange> minor)
    {
        if (!critical.Any() && !major.Any() && !minor.Any())
        {
            return "No changes affect this tenant's active features. Release primarily contains updates to features this tenant doesn't use.";
        }

        var analysis = "";

        if (critical.Any())
        {
            analysis += $"- **CRITICAL Changes**: {critical.Count} affecting {string.Join(", ", critical.Select(c => c.LinkedFeatureId).Distinct())}\n";
            analysis += $"  Tickets: {string.Join(", ", critical.Select(c => c.ChangeId))}\n";
            analysis += $"  Deployment Impact: {string.Join(", ", critical.Select(c => c.DeploymentImpact).Distinct())}\n";
        }

        if (major.Any())
        {
            analysis += $"- **MAJOR Changes**: {major.Count} affecting {string.Join(", ", major.Select(c => c.LinkedFeatureId).Distinct())}\n";
            analysis += $"  Tickets: {string.Join(", ", major.Select(c => c.ChangeId))}\n";
        }

        if (minor.Any())
        {
            analysis += $"- **MINOR Changes**: {minor.Count} affecting {string.Join(", ", minor.Select(c => c.LinkedFeatureId).Distinct())}\n";
        }

        // Add contextual recommendation
        if (critical.Any() && tenant.RiskTolerance.ToLower() == "low")
        {
            analysis += "\n**Preliminary Assessment**: LOW risk tolerance + CRITICAL bugs = likely MUST upgrade.";
        }
        else if (critical.Any() && tenant.RiskTolerance.ToLower() == "high")
        {
            analysis += "\n**Preliminary Assessment**: HIGH risk tolerance + CRITICAL bugs = evaluate if bugs affect current usage (may be SHOULD instead of MUST).";
        }
        else if (!critical.Any() && major.Any())
        {
            analysis += "\n**Preliminary Assessment**: No CRITICAL issues, only MAJOR = likely SHOULD or SKIP depending on actual bug impact.";
        }
        else if (!critical.Any() && !major.Any())
        {
            analysis += "\n**Preliminary Assessment**: Only MINOR changes = likely SKIP unless deployment is trivial and benefits are clear.";
        }

        return analysis;
    }

    private UpgradeRecommendation ParseRecommendation(string llmResponse, string tenantId, string releaseVersion)
    {
        _logger.LogDebug("Parsing LLM response for tenant {TenantId}", tenantId);

        try
        {
            // Simple JSON parsing (use System.Text.Json in production)
            var jsonStart = llmResponse.IndexOf('{');
            var jsonEnd = llmResponse.LastIndexOf('}') + 1;

            if (jsonStart == -1 || jsonEnd <= jsonStart)
            {
                _logger.LogError("Failed to find JSON in LLM response for tenant {TenantId}", tenantId);
                return CreateDefaultRecommendation(tenantId, releaseVersion, "Failed to parse LLM response - no JSON found");
            }

            var json = llmResponse.Substring(jsonStart, jsonEnd - jsonStart);
            _logger.LogDebug("Extracted JSON for tenant {TenantId}: {Json}", tenantId, json);

            var result = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);

            if (result == null)
            {
                _logger.LogError("Failed to deserialize JSON for tenant {TenantId}", tenantId);
                return CreateDefaultRecommendation(tenantId, releaseVersion, "Failed to parse LLM response - deserialization failed");
            }

            var recommendation = new UpgradeRecommendation
            {
                TenantId = tenantId,
                ReleaseVersion = releaseVersion,
                Recommendation = result["recommendation"]?.ToString() ?? "SKIP",
                Reasoning = result["reasoning"]?.ToString() ?? "",
                AffectedFeatures = System.Text.Json.JsonSerializer.Deserialize<List<string>>(result["affectedFeatures"]?.ToString() ?? "[]") ?? new(),
                EstimatedImpact = result["estimatedImpact"]?.ToString() ?? "low"
            };

            _logger.LogInformation("Successfully parsed recommendation for tenant {TenantId}: {Recommendation}",
                tenantId, recommendation.Recommendation);

            return recommendation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while parsing LLM response for tenant {TenantId}", tenantId);
            return CreateDefaultRecommendation(tenantId, releaseVersion, $"Failed to parse LLM response: {ex.Message}");
        }
    }

    private UpgradeRecommendation CreateDefaultRecommendation(string tenantId, string releaseVersion, string errorMessage)
    {
        _logger.LogWarning("Creating default SKIP recommendation for tenant {TenantId} due to: {ErrorMessage}",
            tenantId, errorMessage);

        return new UpgradeRecommendation
        {
            TenantId = tenantId,
            ReleaseVersion = releaseVersion,
            Recommendation = "SKIP",
            Reasoning = errorMessage,
            AffectedFeatures = new(),
            EstimatedImpact = "low"
        };
    }
}