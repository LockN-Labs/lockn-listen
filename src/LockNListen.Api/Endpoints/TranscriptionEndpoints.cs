using LockNListen.Domain.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace LockNListen.Api.Endpoints;

public static class TranscriptionEndpoints
{
    public static void MapTranscriptionEndpoints(this IEndpointRouteBuilder app)
    {
        // POST /api/transcribe - raw audio body (octet-stream)
        app.MapPost("/api/transcribe", async (
            HttpRequest request,
            ISttService sttService,
            IReceiptLogger receiptLogger,
            IOptions<WhisperOptions> options,
            CancellationToken ct) =>
        {
            // File size validation
            if (request.ContentLength > 100 * 1024 * 1024)
                return Results.BadRequest(new { error = "File size exceeds 100MB limit" });

            try
            {
                using var ms = new MemoryStream();
                await request.Body.CopyToAsync(ms, ct);
                var audioData = ms.ToArray();

                // Format validation
                if (!IsValidAudioFormat(audioData))
                    return Results.BadRequest(new { error = "Unsupported audio format. Supported: WAV, FLAC, MP3, MP4" });

                var language = request.Query["language"].FirstOrDefault();
                var startTime = DateTime.UtcNow;
                var result = await sttService.TranscribeAsync(audioData, language, ct);
                var latency = DateTime.UtcNow - startTime;

                // Log receipt
                _ = receiptLogger.LogTranscriptionReceiptAsync(result.Duration, options.Value.ModelSize, latency, result.DetectedLanguage);

                return Results.Ok(new
                {
                    text = result.Text,
                    language = result.DetectedLanguage,
                    confidence = result.Confidence,
                    duration = result.Duration.TotalSeconds,
                    segments = result.Segments.Select(s => new
                    {
                        start = s.Start.TotalSeconds,
                        end = s.End.TotalSeconds,
                        text = s.Text,
                        confidence = s.Confidence
                    }).ToList()
                });
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("pool") || ex.Message.Contains("semaphore"))
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Transcription failed: {ex.Message}", statusCode: 500);
            }
        })
        .WithName("TranscribeAudio")
        .WithTags("Transcription")
        .WithOpenApi();

        // POST /api/transcribe/file - multipart file upload
        app.MapPost("/api/transcribe/file", async (
            IFormFile file,
            ISttService sttService,
            IReceiptLogger receiptLogger,
            IOptions<WhisperOptions> options,
            string? language,
            CancellationToken ct) =>
        {
            // File size validation
            if (file.Length > 100 * 1024 * 1024)
                return Results.BadRequest(new { error = "File size exceeds 100MB limit" });

            try
            {
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms, ct);
                var audioData = ms.ToArray();

                // Format validation
                if (!IsValidAudioFormat(audioData))
                    return Results.BadRequest(new { error = "Unsupported audio format. Supported: WAV, FLAC, MP3, MP4" });

                var startTime = DateTime.UtcNow;
                var result = await sttService.TranscribeAsync(audioData, language, ct);
                var latency = DateTime.UtcNow - startTime;

                // Log receipt
                _ = receiptLogger.LogTranscriptionReceiptAsync(result.Duration, options.Value.ModelSize, latency, result.DetectedLanguage);

                return Results.Ok(new
                {
                    text = result.Text,
                    language = result.DetectedLanguage,
                    confidence = result.Confidence,
                    duration = result.Duration.TotalSeconds,
                    segments = result.Segments.Select(s => new
                    {
                        start = s.Start.TotalSeconds,
                        end = s.End.TotalSeconds,
                        text = s.Text,
                        confidence = s.Confidence
                    }).ToList()
                });
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("pool") || ex.Message.Contains("semaphore"))
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Transcription failed: {ex.Message}", statusCode: 500);
            }
        })
        .DisableAntiforgery()
        .WithName("TranscribeFile")
        .WithTags("Transcription")
        .WithOpenApi();
    }

    private static bool IsValidAudioFormat(byte[] data)
    {
        if (data.Length < 4) return false;

        // WAV: RIFF header
        if (data[0] == 'R' && data[1] == 'I' && data[2] == 'F' && data[3] == 'F')
            return true;

        // FLAC: fLaC header
        if (data[0] == 'f' && data[1] == 'L' && data[2] == 'a' && data[3] == 'C')
            return true;

        // MP3: sync word or ID3 tag
        if ((data[0] == 0xFF && (data[1] & 0xE0) == 0xE0) ||
            (data[0] == 'I' && data[1] == 'D' && data[2] == '3'))
            return true;

        // MP4/M4A: ftyp box
        if (data.Length >= 8 && data[4] == 'f' && data[5] == 't' && data[6] == 'y' && data[7] == 'p')
            return true;

        return false;
    }
}