using Ai.Orchestrator.Models;
using Azure;
using Azure.AI.OpenAI;
using OpenAI.Embeddings;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ai.Orchestrator.Services;

public class QdrantRetrieverService
{
    private readonly QdrantClient _qdrantClient;
    private readonly string _collectionName;
    private readonly EmbeddingClient _embeddingClient;

    public QdrantRetrieverService(IConfiguration configuration)
    {
        var qdrantUrl = configuration["Qdrant:Url"] ?? throw new ArgumentNullException("Qdrant:Url");
        var apiKey = configuration["Qdrant:ApiKey"] ?? throw new ArgumentNullException("Qdrant:ApiKey");

        var uri = new Uri(qdrantUrl);
        var host = uri.Host;
        var port = uri.Port != -1 && uri.Port != 443 ? uri.Port : 6334; // Default Qdrant gRPC port

        _qdrantClient = new QdrantClient(
            host: host,
            port: port,
            https: true, // Enable HTTPS for Qdrant Cloud
            apiKey: apiKey
        );
        _collectionName = configuration["Qdrant:CollectionName"] ?? "jira_tickets";

        // Initialize Azure OpenAI embedding client
        var azureEndpoint = configuration["AzureOpenAI:Endpoint"] ?? throw new ArgumentNullException("AzureOpenAI:Endpoint");
        var azureApiKey = configuration["AzureOpenAI:ApiKey"] ?? throw new ArgumentNullException("AzureOpenAI:ApiKey");
        var embeddingDeployment = configuration["AzureOpenAI:EmbeddingDeploymentName"] ?? throw new ArgumentNullException("AzureOpenAI:EmbeddingDeploymentName");

        var azureClient = new AzureOpenAIClient(new Uri(azureEndpoint), new AzureKeyCredential(azureApiKey));
        _embeddingClient = azureClient.GetEmbeddingClient(embeddingDeployment);
    }

    public async Task<List<JiraTicket>> GetRelevantTicketsAsync(string releaseVersion, List<string> activeFeatures, TenantProfile tenant)
    {
        var allTickets = new List<(JiraTicket ticket, double score)>();

        // SEMANTIC SEARCH APPROACH: Since we don't have indexes on metadata fields,
        // we'll use multiple semantic queries targeting specific features and contexts

        // 1. Feature-specific semantic searches
        foreach (var feature in activeFeatures)
        {
            var featureQuery = $"{releaseVersion} critical bugs and security issues in {feature} feature";
            var featureTickets = await GetSemanticTicketsAsync(featureQuery, limit: 5);
            foreach (var (ticket, score) in featureTickets)
            {
                allTickets.Add((ticket, score + 0.3)); // Boost feature-specific results
            }
        }

        // 2. Contextual semantic search based on tenant profile
        var contextualQuery = BuildSemanticQuery(tenant, activeFeatures, releaseVersion);
        var contextualTickets = await GetSemanticTicketsAsync(contextualQuery, limit: 10);
        foreach (var (ticket, score) in contextualTickets)
        {
            allTickets.Add((ticket, score));
        }

        // 3. Deduplicate, prioritize, and return top results
        // The semantic search with release version in queries will naturally return relevant tickets
        return allTickets
            .GroupBy(t => t.ticket.TicketId)
            .Select(g => new
            {
                Ticket = g.First().ticket,
                MaxScore = g.Max(x => x.score)
            })
            .OrderByDescending(x => x.MaxScore)
            .Take(15)
            .Select(x => x.Ticket)
            .ToList();
    }

    private async Task<List<(JiraTicket ticket, double score)>> GetSemanticTicketsAsync(string query, int limit = 10)
    {
        var queryVector = await GetEmbeddingAsync(query);

        // Search without filters since we don't have indexes
        var searchResults = await _qdrantClient.SearchAsync(
            collectionName: _collectionName,
            vector: queryVector,
            limit: (ulong)limit
        );

        return searchResults.Select(result =>
            (ticket: ConvertToTicket(result), score: (double)result.Score)
        ).ToList();
    }

    private string BuildSemanticQuery(TenantProfile tenant, List<string> activeFeatures, string releaseVersion)
    {
        // Build a rich semantic query based on tenant context
        var featureContext = string.Join(", ", activeFeatures);

        var criticalityContext = tenant.RiskTolerance.ToLower() switch
        {
            "low" => "Focus on critical security vulnerabilities, data corruption risks, and payment failures",
            "medium" => "Focus on critical bugs, security issues, and major feature improvements",
            "high" => "Focus on severe production-breaking issues and critical security vulnerabilities only",
            _ => "Focus on important bugs and security issues"
        };

        var usageContext = tenant.UsagePattern.ToLower() switch
        {
            "high" => "High-volume production usage - prioritize stability and performance issues",
            "medium" => "Standard production usage - balance between stability and new features",
            "low" => "Light usage - focus on security and critical fixes only",
            _ => "Standard usage patterns"
        };

        return $@"{releaseVersion} critical bugs and security vulnerabilities affecting: {featureContext}.
{criticalityContext}.
{usageContext}.
Include: payment processing failures, authentication issues, subscription management bugs,
billing calculation errors, data loss risks, compliance violations.";
    }

    private JiraTicket ConvertToTicket(ScoredPoint result)
    {
        var ticket = new JiraTicket
        {
            TicketId = result.Payload.ContainsKey("issue_key") ? result.Payload["issue_key"].StringValue : "",
            Type = result.Payload.ContainsKey("issue_type") ? result.Payload["issue_type"].StringValue : "",
            Summary = result.Payload.ContainsKey("summary") ? result.Payload["summary"].StringValue : "",
            Description = result.Payload.ContainsKey("description") ? result.Payload["description"].StringValue : "",
            AffectedFeatures = result.Payload.ContainsKey("linked_feature_id") && !string.IsNullOrEmpty(result.Payload["linked_feature_id"].StringValue)
                ? new List<string> { result.Payload["linked_feature_id"].StringValue }
                : new List<string>()
        };

        // Fallback: Parse from content field if summary/description not in metadata (for old data)
        if (string.IsNullOrEmpty(ticket.Summary) && result.Payload.ContainsKey("content"))
        {
            var content = result.Payload["content"].StringValue;
            var summaryStart = content.IndexOf("Summary: ");
            if (summaryStart != -1)
            {
                summaryStart += "Summary: ".Length;
                var summaryEnd = content.IndexOf("\n", summaryStart);
                if (summaryEnd != -1)
                {
                    ticket.Summary = content.Substring(summaryStart, summaryEnd - summaryStart).Trim();
                }
            }
        }

        if (string.IsNullOrEmpty(ticket.Description) && result.Payload.ContainsKey("content"))
        {
            var content = result.Payload["content"].StringValue;
            var descStart = content.IndexOf("Description:");
            if (descStart != -1)
            {
                descStart += "Description:".Length;
                var descEnd = content.IndexOf("\n\nResolution:", descStart);
                if (descEnd != -1)
                {
                    ticket.Description = content.Substring(descStart, descEnd - descStart).Trim();
                }
            }
        }

        return ticket;
    }

    private async Task<float[]> GetEmbeddingAsync(string text)
    {
        var embeddingResult = await _embeddingClient.GenerateEmbeddingAsync(text);
        return embeddingResult.Value.ToFloats().ToArray();
    }
}