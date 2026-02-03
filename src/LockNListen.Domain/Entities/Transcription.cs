namespace LockNListen.Domain.Entities;

/// <summary>
/// A stored transcription record.
/// </summary>
public class Transcription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Text { get; set; }
    public string? Language { get; set; }
    public double Confidence { get; set; }
    public TimeSpan AudioDuration { get; set; }
    public string? SourceFile { get; set; }
    public string? SpeakerId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation
    public ICollection<TranscriptionSegment> Segments { get; set; } = new List<TranscriptionSegment>();
}

/// <summary>
/// A segment within a transcription (for diarization support).
/// </summary>
public class TranscriptionSegment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TranscriptionId { get; set; }
    public required string Text { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public double Confidence { get; set; }
    public string? SpeakerId { get; set; }

    // Navigation
    public Transcription Transcription { get; set; } = null!;
}
