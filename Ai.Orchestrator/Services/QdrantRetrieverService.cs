using Ai.Orchestrator.Models;
using Azure;
using Azure.AI.OpenAI;
using OpenAI.Embeddings;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using System;

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

    public async Task<List<JiraTicket>> GetRelevantTicketsAsync(string releaseVersion, List<string> activeFeatures)
    {
        // Create query from active features
        var queryText = string.Join(" ", activeFeatures);

        // Get embedding for the query
        var queryVector = await GetEmbeddingAsync(queryText);

        // Search Qdrant without release_version filter (since it's not indexed)
        // The release context will be provided by the v1.6.0.json metadata instead
        var searchResults = await _qdrantClient.SearchAsync(
            collectionName: _collectionName,
            vector: queryVector,
            limit: 20
        );

        // Convert to JiraTicket objects with safe field access
        return searchResults.Select(result =>
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
        }).ToList();
    }

    private async Task<float[]> GetEmbeddingAsync(string text)
    {
        var embeddingResult = await _embeddingClient.GenerateEmbeddingAsync(text);
        return embeddingResult.Value.ToFloats().ToArray();
    }
}