using Ai.Orchestrator.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure HttpClient with proper timeout for Azure services
builder.Services.AddHttpClient();

builder.Services.AddSingleton<QdrantRetrieverService>();
builder.Services.AddSingleton<LLMReasoningService>();
builder.Services.AddSingleton<RecommendationEngine>();

var app = builder.Build();

// Configure the HTTP request pipeline.

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();