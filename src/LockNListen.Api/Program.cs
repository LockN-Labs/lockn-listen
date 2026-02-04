using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using LockNListen.Api.Endpoints;
using LockNListen.Api.Middleware;
using LockNListen.Domain.Models;
using LockNListen.Domain.Services;
using LockNListen.Infrastructure.Auth;
using LockNListen.Infrastructure.Services;
using LockNListen.Infrastructure.Sound;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.Configure<SoundClassifierOptions>(builder.Configuration.GetSection("SoundClassifier"));

builder.Services.AddOpenTelemetry()
    .WithTracing(tracerProviderBuilder =>
    {
        tracerProviderBuilder
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("lockn-listen"))
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter(options =>
            {
                var endpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? "http://localhost:4317";
                options.Endpoint = new Uri(endpoint);
            });
    });

builder.Services.AddSingleton<ISttService, WhisperSttService>();
builder.Services.AddSingleton<ISoundClassifier, OnnxSoundClassifier>();

// Add API Key services
builder.Services.Configure<ApiKeyOptions>(builder.Configuration.GetSection("ApiKey"));
builder.Services.AddScoped<IApiKeyRepository, InMemoryApiKeyRepository>();
builder.Services.AddScoped<IApiKeyService, ApiKeyService>();

// Add HTTP client for lockn-apikeys service
builder.Services.AddHttpClient<ApiKeyHttpClient>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Add API Key validation middleware (before other routes)
app.UseMiddleware<ApiKeyValidationMiddleware>();

// Health check
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "lockn-listen" }))
    .WithTags("Health")
    .WithOpenApi();

// Map authentication endpoints
app.MapAuthEndpoints();

// Map transcription endpoints (LOC-41)
app.MapTranscriptionEndpoints();

// Map classification endpoints (LOC-42)
app.MapClassifyEndpoints();

// Map classification WebSocket endpoints (LOC-43)
app.UseWebSockets(options =>
{
    // Ensure WebSocketOptions with KeepAliveInterval = TimeSpan.FromSeconds(30)
    options.KeepAliveInterval = TimeSpan.FromSeconds(30);
});
app.MapClassificationWebSocketEndpoints();

app.Run();
