using EmbeddingService.Services;

namespace EmbeddingService;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly JiraTicketProcessor _processor;
    private readonly IHostApplicationLifetime _lifetime;

    public Worker(
        ILogger<Worker> logger,
        JiraTicketProcessor processor,
        IHostApplicationLifetime lifetime)
    {
        _logger = logger;
        _processor = processor;
        _lifetime = lifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Embedding Service started at: {Time}", DateTimeOffset.Now);

            // Get data directory path
            var dataDirectory = Path.Combine(AppContext.BaseDirectory, "Data");

            if (!Directory.Exists(dataDirectory))
            {
                _logger.LogError("Data directory not found: {Directory}", dataDirectory);
                _lifetime.StopApplication();
                return;
            }

            // Process all tickets once
            await _processor.ProcessAllTicketsAsync(dataDirectory, stoppingToken);

            _logger.LogInformation("Embedding Service completed successfully at: {Time}", DateTimeOffset.Now);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in Embedding Service");
        }
        finally
        {
            // Stop the application after processing completes
            _lifetime.StopApplication();
        }
    }
}
