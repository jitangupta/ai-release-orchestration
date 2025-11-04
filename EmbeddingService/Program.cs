using EmbeddingService;
using EmbeddingService.Services;

var builder = Host.CreateApplicationBuilder(args);

// Register services
builder.Services.AddSingleton<AzureOpenAIService>();
builder.Services.AddSingleton<QdrantService>();
builder.Services.AddSingleton<JiraTicketProcessor>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();