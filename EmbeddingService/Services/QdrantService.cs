using Qdrant.Client.Grpc;
using Qdrant.Client;
using EmbeddingService.Models;

namespace EmbeddingService.Services;

public class QdrantService
{
    private readonly QdrantClient _client;
    private readonly string _collectionName;
    private readonly ILogger<QdrantService> _logger;

    public QdrantService(IConfiguration configuration, ILogger<QdrantService> logger)
    {
        _logger = logger;

        var url = configuration["Qdrant:Url"]
            ?? throw new InvalidOperationException("Qdrant Url not configured");
        var apiKey = configuration["Qdrant:ApiKey"]
            ?? throw new InvalidOperationException("Qdrant ApiKey not configured");
        _collectionName = configuration["Qdrant:CollectionName"]
            ?? throw new InvalidOperationException("Qdrant CollectionName not configured");

        _client = new QdrantClient(new Uri(url), apiKey: apiKey);

        _logger.LogInformation("Qdrant Service initialized for collection: {Collection}", _collectionName);
    }

    /// <summary>
    /// Creates collection if it doesn't exist.
    /// Collection stores 1536-dimension vectors (text-embedding-3-small).
    /// </summary>
    public async Task EnsureCollectionExistsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var collections = await _client.ListCollectionsAsync(cancellationToken);

            if (collections.Any(c => c == _collectionName))
            {
                _logger.LogInformation("Collection '{Collection}' already exists", _collectionName);
                return;
            }

            _logger.LogInformation("Creating collection '{Collection}'", _collectionName);

            await _client.CreateCollectionAsync(
                collectionName: _collectionName,
                vectorsConfig: new VectorParams
                {
                    Size = 1536, // text-embedding-3-small dimension
                    Distance = Distance.Cosine // Cosine similarity for semantic search
                },
                cancellationToken: cancellationToken
            );

            _logger.LogInformation("Collection '{Collection}' created successfully", _collectionName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure collection exists");
            throw;
        }
    }

    /// <summary>
    /// Inserts a single ticket with its embedding vector into Qdrant.
    /// </summary>
    public async Task UpsertTicketAsync(
        JiraTicket ticket,
        float[] embedding,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var pointId = new PointId { Num = GetStableNumericId(ticket.IssueKey) };

            var point = new PointStruct
            {
                Id = pointId,
                Vectors = embedding
            };

            // Build payload using Qdrant's Value type
            foreach (var kv in ticket.GetMetadata())
            {
                point.Payload.Add(kv.Key, ConvertToQdrantValue(kv.Value));
            }
            point.Payload.Add("content", ConvertToQdrantValue(ticket.GetEmbeddingContent()));

            await _client.UpsertAsync(
                collectionName: _collectionName,
                points: new[] { point },
                cancellationToken: cancellationToken
            );

            _logger.LogDebug("Upserted ticket {IssueKey} with ID {PointId}", ticket.IssueKey, pointId.Num);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upsert ticket {IssueKey}", ticket.IssueKey);
            throw;
        }
    }

    /// <summary>
    /// Converts string to stable numeric ID using hash.
    /// </summary>
    private static ulong GetStableNumericId(string key)
    {
        var hash = key.GetHashCode(StringComparison.Ordinal);
        return unchecked((ulong)(uint)hash);
    }

    /// <summary>
    /// Converts .NET objects to Qdrant.Client.Grpc.Value format.
    /// CRITICAL: Must use Qdrant.Client.Grpc.Value, NOT Google.Protobuf.WellKnownTypes.Value
    /// </summary>
    private static Qdrant.Client.Grpc.Value ConvertToQdrantValue(object obj)
    {
        return obj switch
        {
            null => new Qdrant.Client.Grpc.Value { NullValue = 0 },
            string s => new Qdrant.Client.Grpc.Value { StringValue = s },
            bool b => new Qdrant.Client.Grpc.Value { BoolValue = b },
            int i => new Qdrant.Client.Grpc.Value { IntegerValue = i },
            long l => new Qdrant.Client.Grpc.Value { IntegerValue = l },
            double d => new Qdrant.Client.Grpc.Value { DoubleValue = d },
            float f => new Qdrant.Client.Grpc.Value { DoubleValue = f },
            List<string> arr => CreateQdrantListValue(arr),
            IEnumerable<string> arr => CreateQdrantListValue(arr.ToList()),
            _ => new Qdrant.Client.Grpc.Value { StringValue = obj.ToString() ?? string.Empty }
        };
    }

    private static Qdrant.Client.Grpc.Value CreateQdrantListValue(List<string> items)
    {
        var listValue = new ListValue();
        foreach (var item in items)
        {
            listValue.Values.Add(new Qdrant.Client.Grpc.Value { StringValue = item });
        }
        return new Qdrant.Client.Grpc.Value { ListValue = listValue };
    }

    /// <summary>
    /// Batch upsert multiple tickets for better performance.
    /// </summary>
    public async Task UpsertTicketsBatchAsync(
        List<(JiraTicket ticket, float[] embedding)> ticketsWithEmbeddings,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var points = new List<PointStruct>();

            foreach (var item in ticketsWithEmbeddings)
            {
                var pointId = new PointId { Num = GetStableNumericId(item.ticket.IssueKey) };

                var point = new PointStruct
                {
                    Id = pointId,
                    Vectors = item.embedding
                };

                foreach (var kv in item.ticket.GetMetadata())
                {
                    point.Payload.Add(kv.Key, ConvertToQdrantValue(kv.Value));
                }
                point.Payload.Add("content", ConvertToQdrantValue(item.ticket.GetEmbeddingContent()));

                points.Add(point);
            }

            await _client.UpsertAsync(
                collectionName: _collectionName,
                points: points,
                cancellationToken: cancellationToken
            );

            _logger.LogInformation("Batch upserted {Count} tickets", points.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to batch upsert tickets");
            throw;
        }
    }

    /// <summary>
    /// Gets total count of vectors in collection.
    /// </summary>
    public async Task<ulong> GetCollectionCountAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var info = await _client.GetCollectionInfoAsync(_collectionName, cancellationToken);
            // PointsCount is ulong, not nullable, so no need for null check
            return info.PointsCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get collection count");
            throw;
        }
    }

    /// <summary>
    /// Semantic search: finds tickets similar to query text.
    /// </summary>
    public async Task<List<ScoredPoint>> SearchSimilarTicketsAsync(
        float[] queryEmbedding,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var results = await _client.SearchAsync(
                collectionName: _collectionName,
                vector: queryEmbedding,
                limit: (ulong)limit,
                cancellationToken: cancellationToken
            );

            return results.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search similar tickets");
            throw;
        }
    }
}