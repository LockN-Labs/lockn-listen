namespace LockNListen.Domain.Interfaces;

/// <summary>
/// Detects and classifies sound events in audio streams.
/// </summary>
public interface ISoundEventDetector
{
    /// <summary>
    /// Analyzes audio for known sound events.
    /// </summary>
    /// <param name="audioData">Raw audio bytes</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of detected sound events</returns>
    Task<IReadOnlyList<SoundEvent>> DetectEventsAsync(
        byte[] audioData,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a callback for real-time event detection (future).
    /// </summary>
    Task SubscribeToEventsAsync(
        Func<SoundEvent, Task> callback,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// A detected sound event with classification.
/// </summary>
public record SoundEvent
{
    public required string Category { get; init; }  // e.g., "doorbell", "alarm", "speech"
    public required string Label { get; init; }     // e.g., "Ring doorbell", "Smoke alarm"
    public double Confidence { get; init; }
    public TimeSpan Timestamp { get; init; }
    public TimeSpan Duration { get; init; }
}

/// <summary>
/// Standard sound event categories.
/// </summary>
public static class SoundEventCategories
{
    public const string Speech = "speech";
    public const string Music = "music";
    public const string Doorbell = "doorbell";
    public const string Alarm = "alarm";
    public const string GlassBreak = "glass_break";
    public const string Dog = "dog";
    public const string Baby = "baby";
    public const string Vehicle = "vehicle";
    public const string Unknown = "unknown";
}
