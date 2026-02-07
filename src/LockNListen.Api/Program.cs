using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using LockNListen.Api.Endpoints;
using LockNListen.Api.Errors;
using LockNListen.Api.Middleware;
using LockNListen.Domain.Interfaces;
using LockNListen.Domain.Models;
using LockNListen.Domain.Services;
using LockNListen.Infrastructure.Auth;
using LockNListen.Infrastructure.Services;
using LockNListen.Infrastructure.Sound;
using LockNListen.Infrastructure.Speech;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.Configure<SoundClassifierOptions>(builder.Configuration.GetSection("SoundClassifier"));
builder.Services.Configure<WhisperOptions>(builder.Configuration.GetSection("Whisper"));
builder.Services.Configure<FasterWhisperOptions>(builder.Configuration.GetSection("FasterWhisper"));

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

// Streaming STT service (faster-whisper HTTP client)
builder.Services.AddHttpClient<IStreamingSttService, FasterWhisperSttService>();

// Add API Key services
builder.Services.Configure<ApiKeyOptions>(builder.Configuration.GetSection("ApiKey"));
builder.Services.AddScoped<IApiKeyRepository, InMemoryApiKeyRepository>();
builder.Services.AddScoped<IApiKeyService, ApiKeyService>();

// Add HTTP client for lockn-apikeys service
builder.Services.AddHttpClient<ApiKeyHttpClient>();

// Add HTTP client for LockNLogger
builder.Services.AddHttpClient("LockNLogger", client =>
{
    // Base URL will be configured via appsettings.json
});

// Add receipt logger
builder.Services.AddSingleton<IReceiptLogger, LockNLoggerClient>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Global exception handling (must be first in pipeline)
app.UseGlobalExceptionHandler();

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
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)
});
app.MapClassificationWebSocketEndpoints();

// Map STT WebSocket endpoint (LOC-164)
app.MapSttWebSocketEndpoints();

app.Run();