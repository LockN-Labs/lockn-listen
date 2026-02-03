namespace LockNListen.Domain.Interfaces;

/// <summary>
/// Speech-to-Text transcription service interface.
/// </summary>
public interface ISttService
{
    /// <summary>
    /// Transcribes audio data to text.
    /// </summary>
    /// <param name="audioData">Raw audio bytes (WAV, MP3, etc.)</param>
    /// <param name="language">Optional language hint (ISO 639-1 code, e.g., "en")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Transcription result with text and metadata</returns>
    Task<TranscriptionResult> TranscribeAsync(
        byte[] audioData,
        string? language = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Transcribes audio from a stream.
    /// </summary>
    Task<TranscriptionResult> TranscribeAsync(
        Stream audioStream,
        string? language = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a transcription operation.
/// </summary>
public record TranscriptionResult
{
    public required string Text { get; init; }
    public double Confidence { get; init; }
    public TimeSpan Duration { get; init; }
    public string? DetectedLanguage { get; init; }
    public IReadOnlyList<TranscriptionSegment>? Segments { get; init; }
}

/// <summary>
/// A segment of transcribed audio with timing information.
/// </summary>
public record TranscriptionSegment
{
    public required string Text { get; init; }
    public TimeSpan Start { get; init; }
    public TimeSpan End { get; init; }
    public double Confidence { get; init; }
}
