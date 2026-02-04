namespace LockNListen.Infrastructure.Services;

/// <summary>
/// Configuration options for Whisper STT service.
/// </summary>
public class WhisperOptions
{
    /// <summary>
    /// The Whisper model size to use (tiny, base, small, medium, large).
    /// </summary>
    public string ModelSize { get; set; } = "base";

    /// <summary>
    /// Path to the model files directory.
    /// </summary>
    public string? ModelPath { get; set; }

    /// <summary>
    /// Enable GPU acceleration if available.
    /// </summary>
    public bool UseGpu { get; set; } = true;
}
