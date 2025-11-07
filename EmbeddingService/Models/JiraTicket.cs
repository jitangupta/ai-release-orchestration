using System.Text.Json.Serialization;

namespace EmbeddingService.Models;

public class JiraTicket
{
    [JsonPropertyName("issue_key")]
    public string IssueKey { get; set; } = string.Empty;

    [JsonPropertyName("issue_type")]
    public string IssueType { get; set; } = string.Empty;

    [JsonPropertyName("parent_issue_key")]
    public string? ParentIssueKey { get; set; }

    [JsonPropertyName("subtasks")]
    public List<string> Subtasks { get; set; } = new();

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("linked_feature_id")]
    public string LinkedFeatureId { get; set; } = string.Empty;

    [JsonPropertyName("priority")]
    public string Priority { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("resolution_details")]
    public string ResolutionDetails { get; set; } = string.Empty;

    [JsonPropertyName("impacted_modules")]
    public List<string> ImpactedModules { get; set; } = new();

    [JsonPropertyName("customer_context")]
    public CustomerContext? CustomerContext { get; set; }

    [JsonPropertyName("fix_version")]
    public string FixVersion { get; set; } = string.Empty;

    [JsonPropertyName("release_notes_excerpt")]
    public string? ReleaseNotesExcerpt { get; set; }

    /// <summary>
    /// Generates the text content that will be embedded.
    /// This is what the LLM will use for RAG retrieval.
    /// </summary>
    public string GetEmbeddingContent()
    {
        var content = $"""
                Release Version: {FixVersion}
            Issue Key: {IssueKey}
            Type: {IssueType}
            Feature: {LinkedFeatureId}
            Priority: {Priority}
            Summary: {Summary}

            Description:
            {Description}

            Resolution:
            {ResolutionDetails}

            Impacted Modules: {string.Join(", ", ImpactedModules)}
            """;

        if (!string.IsNullOrEmpty(ReleaseNotesExcerpt))
        {
            content += $"\n\nRelease Notes:\n{ReleaseNotesExcerpt}";
        }

        return content;
    }

    /// <summary>
    /// Metadata stored in Qdrant for filtering and retrieval.
    /// </summary>
    public Dictionary<string, object> GetMetadata()
    {
        var metadata = new Dictionary<string, object>
        {
            ["issue_key"] = IssueKey,
            ["issue_type"] = IssueType,
            ["summary"] = Summary,
            ["description"] = Description,
            ["linked_feature_id"] = LinkedFeatureId,
            ["priority"] = Priority,
            ["status"] = Status,
            ["fix_version"] = FixVersion,
            ["impacted_modules"] = ImpactedModules
        };

        if (ParentIssueKey != null)
        {
            metadata["parent_issue_key"] = ParentIssueKey;
        }

        if (CustomerContext?.CustomerId != null)
        {
            metadata["customer_id"] = CustomerContext.CustomerId;
            metadata["is_key_account"] = CustomerContext.IsKeyAccount;
        }

        return metadata;
    }
}

public class CustomerContext
{
    [JsonPropertyName("customer_id")]
    public string? CustomerId { get; set; }

    [JsonPropertyName("is_key_account")]
    public bool IsKeyAccount { get; set; }
}