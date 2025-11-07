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

**CRITICAL INSTRUCTION**: Use PROBABILISTIC REASONING. Don't just check if a feature has a bug - assess the LIKELIHOOD that this specific tenant is experiencing the bug based on their usage profile, risk tolerance, and bug characteristics.

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

**Probabilistic Decision Framework:**

**MUST Upgrade** = HIGH probability (>70%) tenant is experiencing or will soon experience critical impact:
1. CRITICAL security vulnerability in authentication/payment features they actively use
2. CRITICAL data corruption/loss bugs in features with HIGH daily usage
3. Widespread production-breaking bugs reported by multiple similar tenants
4. Compliance violations with immediate legal/financial consequences
5. LOW risk tolerance + CRITICAL bug in core business feature (subscription/billing)

Examples:
- ✓ MUST: Payment retry bug causing refunds → tenant uses F4_BillingPayments heavily
- ✗ NOT MUST: Proration bug for multi-year contracts → tenant only has monthly subscriptions

**SHOULD Upgrade** = MEDIUM probability (30-70%) of impact OR non-critical improvements worth deploying:
1. MAJOR bugs in frequently used features that affect subset of workflows
2. Edge case CRITICAL bugs tenant might encounter occasionally
3. MEDIUM risk tolerance + CRITICAL bugs in lightly-used features
4. Multiple MAJOR improvements that benefit tenant's workflow
5. HIGH usage tenants with performance/stability MAJOR fixes

Examples:
- ✓ SHOULD: OAuth refresh bug → tenant uses F1 but may not hit edge case frequently
- ✓ SHOULD: MRR calculation bug → tenant uses F8 reporting but not mission-critical
- ✗ NOT SHOULD: Edge case bugs tenant unlikely to encounter

**SKIP Upgrade** = LOW probability (<30%) of impact OR minimal benefit vs. deployment cost:
1. Only MINOR/PATCH bugs in their features (UI typos, cosmetic issues)
2. MAJOR/CRITICAL bugs in features they DON'T use at all
3. HIGH risk tolerance + edge case bugs in non-core features
4. New features tenant hasn't requested (no value, just deployment risk)
5. LOW daily usage + only minor improvements
6. MEDIUM/HIGH deployment complexity + minimal tangible benefit

Examples:
- ✓ SKIP: CRITICAL bug in F6_InvoicingTax → tenant doesn't use F6
- ✓ SKIP: MAJOR bug in F5_UsageTracking quota reset → tenant has LOW usage, unlikely to hit quotas
- ✓ SKIP: MINOR bug in F2_UserManagement → tenant has HIGH risk tolerance, cosmetic issue
- ✓ SKIP: New F10_TenantAdmin features → tenant hasn't requested, no immediate value

**Risk Tolerance Interpretation:**
- LOW risk tolerance: Defensive - upgrade for CRITICAL bugs even if unlikely to impact (probability >30%)
- MEDIUM risk tolerance: Balanced - upgrade if probability of impact >50%
- HIGH risk tolerance: Aggressive - only upgrade if probability of impact >70% OR severe consequences

**Usage Pattern Interpretation:**
- HIGH usage (>70): Higher probability of hitting bugs → SHOULD for MAJOR bugs
- MEDIUM usage (40-70): Standard probability assessment → focus on common workflows
- LOW usage (<40): Lower probability of hitting bugs → SKIP unless CRITICAL security/data loss

**Bug Likelihood Assessment Checklist:**
1. Is this bug affecting a core workflow tenant uses daily? (YES = +40% probability)
2. Is this bug widespread or an edge case? (Widespread = +30%, Edge case = +5%)
3. Does tenant's profile match the bug's conditions? (Match = +30%, No match = -50%)
4. Has tenant reported similar issues before? (If mentioned = +40%)
5. Is this a data corruption/security bug vs. UX bug? (Data/Security = +20%)

**Current Guidance:**
- Default to SKIP unless you can articulate HIGH or MEDIUM probability of impact
- Bug severity alone is NOT enough - assess tenant-specific likelihood
- Consider: ""Would this tenant notice if we don't upgrade?"" If NO → SKIP
- Consider: ""Is the deployment risk worth the unlikely benefit?"" If NO → SKIP

**Output Format (JSON only):**
{{
  ""recommendation"": ""MUST|SHOULD|SKIP"",
  ""reasoning"": ""State the PROBABILITY of impact and WHY. Example: 'SKIP: Only MAJOR bug in F5_UsageTracking (quota reset). Tenant has LOW usage (35 daily score), unlikely to hit quota limits. HIGH risk tolerance suggests waiting. No CRITICAL bugs in their features.'"",
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

        // Add probabilistic contextual recommendation
        var usageScore = int.Parse(tenant.UsagePattern.ToLower() switch
        {
            "high" => "75",
            "medium" => "50",
            "low" => "30",
            _ => "50"
        });

        analysis += "\n**Probabilistic Impact Assessment:**\n";

        if (critical.Any() && tenant.RiskTolerance.ToLower() == "low")
        {
            analysis += "- LOW risk tolerance + CRITICAL bugs = HIGH impact probability (>70%) → likely MUST upgrade.\n";
            analysis += "- Rationale: Defensive posture requires proactive fixes even for unlikely scenarios.\n";
        }
        else if (critical.Any() && tenant.RiskTolerance.ToLower() == "high")
        {
            analysis += "- HIGH risk tolerance + CRITICAL bugs = Evaluate bug likelihood carefully.\n";
            analysis += "- MUST only if tenant likely experiencing bug NOW (check bug description vs. tenant profile).\n";
            analysis += "- SKIP if bugs are edge cases tenant unlikely to encounter (consider usage patterns).\n";
        }
        else if (!critical.Any() && major.Any() && tenant.RiskTolerance.ToLower() == "high")
        {
            analysis += $"- HIGH risk tolerance + only MAJOR bugs + usage score {usageScore} = LOW impact probability.\n";
            analysis += "- Likely SKIP unless: (1) high-frequency workflows affected, (2) data integrity risk, (3) compliance issue.\n";
        }
        else if (!critical.Any() && major.Any())
        {
            analysis += $"- Only MAJOR issues + {tenant.RiskTolerance} risk tolerance + {tenant.UsagePattern} usage = Assess likelihood.\n";
            analysis += "- SHOULD if: bugs affect daily workflows. SKIP if: edge cases or low usage feature.\n";
        }
        else if (!critical.Any() && !major.Any())
        {
            analysis += "- Only MINOR/PATCH changes = VERY LOW impact probability (<10%).\n";
            analysis += "- Default to SKIP. Cosmetic fixes don't justify deployment risk.\n";
        }

        if (!critical.Any() && !major.Any() && minor.Any())
        {
            analysis += $"\n**Recommendation Hint**: With only MINOR changes and {tenant.RiskTolerance.ToUpper()} risk tolerance, this is a clear SKIP candidate.";
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