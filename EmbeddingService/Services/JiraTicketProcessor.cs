using EmbeddingService.Models;
using System.Text.Json;

namespace EmbeddingService.Services;

public class JiraTicketProcessor
{
    private readonly AzureOpenAIService _openAIService;
    private readonly QdrantService _qdrantService;
    private readonly ILogger<JiraTicketProcessor> _logger;

    public JiraTicketProcessor(
        AzureOpenAIService openAIService,
        QdrantService qdrantService,
        ILogger<JiraTicketProcessor> logger)
    {
        _openAIService = openAIService;
        _qdrantService = qdrantService;
        _logger = logger;
    }

    /// <summary>
    /// Loads all JSONL files and processes tickets into Qdrant.
    /// </summary>
    public async Task ProcessAllTicketsAsync(string dataDirectory, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting ticket processing from directory: {Directory}", dataDirectory);

        // Ensure Qdrant collection exists
        await _qdrantService.EnsureCollectionExistsAsync(cancellationToken);

        // Load all tickets from JSONL files
        var tickets = new List<JiraTicket>();

        var files = new[] { "r6_stories.jsonl", "r6_bugs.jsonl", "r6_subtasks.jsonl" };

        foreach (var file in files)
        {
            var filePath = Path.Combine(dataDirectory, file);
            var fileTickets = await LoadTicketsFromJsonlAsync(filePath, cancellationToken);
            tickets.AddRange(fileTickets);

            _logger.LogInformation("Loaded {Count} tickets from {File}", fileTickets.Count, file);
        }

        _logger.LogInformation("Total tickets loaded: {Total}", tickets.Count);

        // Process tickets in batches
        var batchSize = 10;
        for (int i = 0; i < tickets.Count; i += batchSize)
        {
            var batch = tickets.Skip(i).Take(batchSize).ToList();

            _logger.LogInformation("Processing batch {Current}/{Total}",
                i / batchSize + 1,
                (tickets.Count + batchSize - 1) / batchSize);

            await ProcessBatchAsync(batch, cancellationToken);
        }

        var finalCount = await _qdrantService.GetCollectionCountAsync(cancellationToken);

        _logger.LogInformation("Processing complete. Total vectors in Qdrant: {Count}", finalCount);
    }

    private async Task ProcessBatchAsync(List<JiraTicket> tickets, CancellationToken cancellationToken)
    {
        var ticketsWithEmbeddings = new List<(JiraTicket ticket, float[] embedding)>();

        foreach (var ticket in tickets)
        {
            try
            {
                var content = ticket.GetEmbeddingContent();
                var embedding = await _openAIService.GenerateEmbeddingAsync(content, cancellationToken);

                ticketsWithEmbeddings.Add((ticket, embedding));

                _logger.LogInformation("Generated embedding for {IssueKey}", ticket.IssueKey);

                // Rate limiting
                await Task.Delay(350, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process ticket {IssueKey}", ticket.IssueKey);
                // Continue processing other tickets
            }
        }

        if (ticketsWithEmbeddings.Any())
        {
            await _qdrantService.UpsertTicketsBatchAsync(ticketsWithEmbeddings, cancellationToken);
        }
    }

    private async Task<List<JiraTicket>> LoadTicketsFromJsonlAsync(string filePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            _logger.LogWarning("File not found: {FilePath}", filePath);
            return new List<JiraTicket>();
        }

        var tickets = new List<JiraTicket>();
        var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                var ticket = JsonSerializer.Deserialize<JiraTicket>(line);
                if (ticket != null)
                {
                    tickets.Add(ticket);
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize line: {Line}", line[..Math.Min(100, line.Length)]);
            }
        }

        return tickets;
    }
}
