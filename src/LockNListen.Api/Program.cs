using LockNListen.Api.Endpoints;
using LockNListen.Domain.Interfaces;
using LockNListen.Domain.Services;
using LockNListen.Infrastructure.Services;
using LockNListen.Infrastructure.Sound;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.Configure<SoundClassifierOptions>(builder.Configuration.GetSection("SoundClassifier"));
builder.Services.AddSingleton<ISttService, WhisperSttService>();
builder.Services.AddSingleton<ISoundClassifier, OnnxSoundClassifier>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline
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

// Map classification endpoints (LOC-42)
app.MapClassifyEndpoints();

app.Run();
