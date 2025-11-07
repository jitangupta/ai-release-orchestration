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

    public LLMReasoningService(IConfiguration configuration)
    {
        var endpoint = configuration["AzureOpenAI:Endpoint"] ?? throw new ArgumentNullException("AzureOpenAI:Endpoint");
        var apiKey = configuration["AzureOpenAI:ApiKey"] ?? throw new ArgumentNullException("AzureOpenAI:ApiKey");
        var deploymentName = configuration["AzureOpenAI:ChatDeploymentName"] ?? throw new ArgumentNullException("AzureOpenAI:ChatDeploymentName");

        var client = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
        _chatClient = client.GetChatClient(deploymentName);
        _outputDirectory = configuration["OutputDirectory"] ?? "Data/generatedprompts";
    }

    public async Task<UpgradeRecommendation> GenerateRecommendationAsync(
        TenantProfile tenant,
        ReleaseContext releaseContext)
    {
        var prompt = BuildPrompt(tenant, releaseContext);
        await SavepromptsAsync(tenant, prompt);
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage("You are an expert system that analyzes software releases and generates upgrade recommendations."),
            new UserChatMessage(prompt)
        };

        var response = await _chatClient.CompleteChatAsync(messages);
        var content = response.Value.Content[0].Text;

        // Parse LLM response (expecting JSON)
        return ParseRecommendation(content, tenant.TenantId, releaseContext.ReleaseVersion);
    }

    private async Task SavepromptsAsync(TenantProfile tenant, string prompt)
    {
        var fileName = $"{tenant.TenantId}_Prompt_{DateTime.UtcNow:yyyyMMddHHmmss}.txt";
        var filePath = Path.Combine(_outputDirectory, fileName);

        await File.WriteAllTextAsync(filePath, prompt);
    }

    private string BuildPrompt(TenantProfile tenant, ReleaseContext releaseContext)
    {
        var ticketsSummary = string.Join("\n", releaseContext.RelevantTickets.Select(t =>
            $"- [{t.Type}] {t.TicketId}: {t.Summary}"));

        var metadata = releaseContext.Metadata;

        // Analyze changes affecting tenant's active features
        var criticalChanges = metadata?.ContentBreakdown
            .Where(c => tenant.ActiveFeatures.Contains(c.LinkedFeatureId) && c.Severity == "CRITICAL")
            .ToList() ?? new();

        var majorChanges = metadata?.ContentBreakdown
            .Where(c => tenant.ActiveFeatures.Contains(c.LinkedFeatureId) && c.Severity == "MAJOR")
            .ToList() ?? new();

        var changesSummary = "";
        if (criticalChanges.Any() || majorChanges.Any())
        {
            changesSummary = $@"
**Changes Affecting Tenant's Active Features:**
- Critical Changes: {criticalChanges.Count} ({string.Join(", ", criticalChanges.Select(c => c.ChangeId))})
- Major Changes: {majorChanges.Count} ({string.Join(", ", majorChanges.Select(c => c.ChangeId))})
";
        }

        return $@"
Analyze this release for tenant upgrade recommendation.

**Tenant Profile:**
- Tenant ID: {tenant.TenantId}
- Active Features: {string.Join(", ", tenant.ActiveFeatures)}
- Usage Pattern: {tenant.UsagePattern}
- Risk Tolerance: {tenant.RiskTolerance}

**Release: {releaseContext.ReleaseVersion}**
- Deployment Complexity: {metadata?.DeploymentComplexity}
- Release Summary: {metadata?.ReleaseSummary}
- Total Changes in Release: {metadata?.ContentBreakdown.Count}
{changesSummary}

**Relevant Tickets (from vector search):**
{ticketsSummary}

**Decision Rules:**
- MUST: Critical security fixes or bugs in tenant's active features, especially for LOW risk tolerance tenants
- SHOULD: Major bug fixes or improvements in active features; consider tenant's risk tolerance and usage pattern
- SKIP: Only changes to features the tenant doesn't use, or for HIGH risk tolerance tenants with minimal impact

Consider:
1. Tenant's risk tolerance (LOW = upgrade for any critical fixes, HIGH = can defer unless urgent)
2. Usage pattern (high usage = more careful evaluation needed)
3. Deployment complexity and impact on tenant's infrastructure

**Output Format (JSON only):**
{{
  ""recommendation"": ""MUST|SHOULD|SKIP"",
  ""reasoning"": ""Brief explanation considering tenant's risk tolerance, usage pattern, and affected features"",
  ""affectedFeatures"": [""feature1"", ""feature2""],
  ""estimatedImpact"": ""high|medium|low""
}}
";
    }

    private UpgradeRecommendation ParseRecommendation(string llmResponse, string tenantId, string releaseVersion)
    {
        // Simple JSON parsing (use System.Text.Json in production)
        var jsonStart = llmResponse.IndexOf('{');
        var jsonEnd = llmResponse.LastIndexOf('}') + 1;
        var json = llmResponse.Substring(jsonStart, jsonEnd - jsonStart);

        var result = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        if (result == null)
        {
            return new UpgradeRecommendation
            {
                TenantId = tenantId,
                ReleaseVersion = releaseVersion,
                Recommendation = "SKIP",
                Reasoning = "Failed to parse LLM response",
                AffectedFeatures = new(),
                EstimatedImpact = "low"
            };
        }

        return new UpgradeRecommendation
        {
            TenantId = tenantId,
            ReleaseVersion = releaseVersion,
            Recommendation = result["recommendation"]?.ToString() ?? "SKIP",
            Reasoning = result["reasoning"]?.ToString() ?? "",
            AffectedFeatures = System.Text.Json.JsonSerializer.Deserialize<List<string>>(result["affectedFeatures"]?.ToString() ?? "[]") ?? new(),
            EstimatedImpact = result["estimatedImpact"]?.ToString() ?? "low"
        };
    }
}