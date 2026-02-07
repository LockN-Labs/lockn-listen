namespace LockNListen.Domain.Interfaces;

/// <summary>
/// Streaming Speech-to-Text service for real-time transcription.
/// </summary>
public interface IStreamingSttService
{
    /// <summary>
    /// Transcribes an audio segment (after VAD detected end of speech).
    /// </summary>
    /// <param name="audioData">PCM audio data (16kHz, 16-bit, mono)</param>
    /// <param name="language">Optional language hint</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Transcription result</returns>
    Task<StreamingTranscriptionResult> TranscribeSegmentAsync(
        byte[] audioData,
        string? language = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result from streaming transcription.
/// </summary>
public record StreamingTranscriptionResult
{
    /// <summary>Transcribed text</summary>
    public required string Text { get; init; }
    
    /// <summary>Detected or specified language</summary>
    public string? Language { get; init; }
    
    /// <summary>Average confidence score</summary>
    public double Confidence { get; init; }
    
    /// <summary>Audio duration in seconds</summary>
    public double DurationSeconds { get; init; }
    
    /// <summary>Whether this is the final result for this segment</summary>
    public bool IsFinal { get; init; } = true;
    
    /// <summary>Unique segment identifier</summary>
    public string SegmentId { get; init; } = Guid.NewGuid().ToString("N")[..8];
}
