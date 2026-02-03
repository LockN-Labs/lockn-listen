using LockNListen.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace LockNListen.Infrastructure.Services;

/// <summary>
/// Speech-to-Text service using Whisper.net (local inference).
/// </summary>
public class WhisperSttService : ISttService
{
    private readonly ILogger<WhisperSttService> _logger;
    // TODO: Add WhisperFactory when Whisper.net package is added

    public WhisperSttService(ILogger<WhisperSttService> logger)
    {
        _logger = logger;
    }

    public async Task<TranscriptionResult> TranscribeAsync(
        byte[] audioData,
        string? language = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Transcribing {Bytes} bytes of audio", audioData.Length);

        // TODO: Implement Whisper inference
        // 1. Convert audioData to WAV if needed
        // 2. Run through Whisper model
        // 3. Return transcription with segments

        await Task.Delay(100, cancellationToken); // Placeholder

        return new TranscriptionResult
        {
            Text = "[Whisper transcription not yet implemented]",
            Confidence = 0.0,
            Duration = TimeSpan.Zero,
            DetectedLanguage = language ?? "en"
        };
    }

    public async Task<TranscriptionResult> TranscribeAsync(
        Stream audioStream,
        string? language = null,
        CancellationToken cancellationToken = default)
    {
        using var ms = new MemoryStream();
        await audioStream.CopyToAsync(ms, cancellationToken);
        return await TranscribeAsync(ms.ToArray(), language, cancellationToken);
    }
}
