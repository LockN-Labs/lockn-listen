using LockNListen.Api.Endpoints;
using LockNListen.Domain.Interfaces;
using LockNListen.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register Listen services
builder.Services.AddSingleton<ISttService, WhisperSttService>();

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Health check
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "lockn-listen" }))
    .WithTags("Health")
    .WithOpenApi();

// Map transcription endpoints (LOC-41)
app.MapTranscriptionEndpoints();

app.Run();
