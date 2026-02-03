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
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "lockn-listen" }));

// STT Endpoints
app.MapPost("/api/transcribe", async (
    HttpRequest request,
    ISttService sttService,
    CancellationToken ct) =>
{
    using var ms = new MemoryStream();
    await request.Body.CopyToAsync(ms, ct);
    
    var language = request.Query["language"].FirstOrDefault();
    var result = await sttService.TranscribeAsync(ms.ToArray(), language, ct);
    
    return Results.Ok(result);
})
.WithName("TranscribeAudio")
.WithOpenApi();

app.MapPost("/api/transcribe/file", async (
    IFormFile file,
    ISttService sttService,
    string? language,
    CancellationToken ct) =>
{
    using var stream = file.OpenReadStream();
    var result = await sttService.TranscribeAsync(stream, language, ct);
    return Results.Ok(result);
})
.DisableAntiforgery()
.WithName("TranscribeFile")
.WithOpenApi();

app.Run();
