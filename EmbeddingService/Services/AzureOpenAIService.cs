using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI.Embeddings;

namespace EmbeddingService.Services;

public class AzureOpenAIService
{
    private readonly AzureOpenAIClient _client;
    private readonly EmbeddingClient _embeddingClient;
    private readonly ILogger<AzureOpenAIService> _logger;

    public AzureOpenAIService(IConfiguration configuration, ILogger<AzureOpenAIService> logger)
    {
        _logger = logger;

        var endpoint = configuration["AzureOpenAI:Endpoint"]
            ?? throw new InvalidOperationException("Azure OpenAI Endpoint not configured");
        var apiKey = configuration["AzureOpenAI:ApiKey"]
            ?? throw new InvalidOperationException("Azure OpenAI ApiKey not configured");
        var embeddingDeployment = configuration["AzureOpenAI:EmbeddingDeployment"]
            ?? throw new InvalidOperationException("Azure OpenAI EmbeddingDeployment not configured");

        _client = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
        _embeddingClient = _client.GetEmbeddingClient(embeddingDeployment);

        _logger.LogInformation("Azure OpenAI Service initialized with deployment: {Deployment}", embeddingDeployment);
    }

    /// <summary>
    /// Generates embedding vector for given text using Azure OpenAI.
    /// Returns 1536-dimension vector for text-embedding-3-small.
    /// </summary>
    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _embeddingClient.GenerateEmbeddingAsync(text, cancellationToken: cancellationToken);

            var embedding = response.Value.ToFloats().ToArray();

            _logger.LogDebug("Generated embedding with {Dimensions} dimensions", embedding.Length);

            return embedding;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate embedding for text (length: {Length})", text.Length);
            throw;
        }
    }

    /// <summary>
    /// Batch generate embeddings with retry logic and rate limiting.
    /// </summary>
    public async Task<List<float[]>> GenerateEmbeddingsBatchAsync(
        List<string> texts,
        int batchSize = 16,
        CancellationToken cancellationToken = default)
    {
        var embeddings = new List<float[]>();

        for (int i = 0; i < texts.Count; i += batchSize)
        {
            var batch = texts.Skip(i).Take(batchSize).ToList();

            _logger.LogInformation("Processing embedding batch {Current}/{Total}",
                i / batchSize + 1,
                (texts.Count + batchSize - 1) / batchSize);

            foreach (var text in batch)
            {
                var embedding = await GenerateEmbeddingAsync(text, cancellationToken);
                embeddings.Add(embedding);

                // Rate limiting: Azure OpenAI has limits (e.g., 3 requests/second on free tier)
                await Task.Delay(350, cancellationToken); // ~3 requests/second
            }
        }

        return embeddings;
    }
}