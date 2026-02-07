using System;
using System.Text.Json.Serialization;

namespace LockNListen.Api.WebSockets;

/// <summary>
/// WebSocket message types for STT streaming.
/// </summary>
public static class SttMessageTypes
{
    public const string Config = "config";
    public const string SpeechStart = "speech_start";
    public const string SpeechEnd = "speech_end";
    public const string Transcript = "transcript";
    public const string Error = "error";
    public const string Ready = "ready";
    public const string VadStatus = "vad_status";
}

/// <summary>
/// Base message structure for WebSocket STT.
/// </summary>
public abstract record SttWebSocketMessage
{
    [JsonPropertyName("type")]
    public abstract string Type { get; }
    
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Configuration message sent by client to configure the session.
/// </summary>
public record SttConfigMessage : SttWebSocketMessage
{
    public override string Type => SttMessageTypes.Config;
    
    /// <summary>Language hint (ISO 639-1 code)</summary>
    [JsonPropertyName("language")]
    public string? Language { get; init; }
    
    /// <summary>Whether to send VAD status updates</summary>
    [JsonPropertyName("send_vad_status")]
    public bool SendVadStatus { get; init; }
    
    /// <summary>Custom VAD sensitivity (0.0-1.0, higher = more sensitive)</summary>
    [JsonPropertyName("vad_sensitivity")]
    public double? VadSensitivity { get; init; }
}

/// <summary>
/// Ready message sent when connection is established.
/// </summary>
public record SttReadyMessage : SttWebSocketMessage
{
    public override string Type => SttMessageTypes.Ready;
    
    [JsonPropertyName("session_id")]
    public required string SessionId { get; init; }
    
    [JsonPropertyName("message")]
    public string Message { get; init; } = "Ready to receive audio. Send 16kHz 16-bit mono PCM.";
}

/// <summary>
/// Indicates speech has started.
/// </summary>
public record SttSpeechStartMessage : SttWebSocketMessage
{
    public override string Type => SttMessageTypes.SpeechStart;
    
    [JsonPropertyName("segment_id")]
    public required string SegmentId { get; init; }
}

/// <summary>
/// Indicates speech has ended and transcription is being processed.
/// </summary>
public record SttSpeechEndMessage : SttWebSocketMessage
{
    public override string Type => SttMessageTypes.SpeechEnd;
    
    [JsonPropertyName("segment_id")]
    public required string SegmentId { get; init; }
    
    [JsonPropertyName("duration_ms")]
    public int DurationMs { get; init; }
}

/// <summary>
/// Transcription result.
/// </summary>
public record SttTranscriptMessage : SttWebSocketMessage
{
    public override string Type => SttMessageTypes.Transcript;
    
    [JsonPropertyName("segment_id")]
    public required string SegmentId { get; init; }
    
    [JsonPropertyName("text")]
    public required string Text { get; init; }
    
    [JsonPropertyName("is_final")]
    public bool IsFinal { get; init; } = true;
    
    [JsonPropertyName("confidence")]
    public double Confidence { get; init; }
    
    [JsonPropertyName("language")]
    public string? Language { get; init; }
    
    [JsonPropertyName("duration_seconds")]
    public double DurationSeconds { get; init; }
}

/// <summary>
/// Error message.
/// </summary>
public record SttErrorMessage : SttWebSocketMessage
{
    public override string Type => SttMessageTypes.Error;
    
    [JsonPropertyName("error")]
    public required string Error { get; init; }
    
    [JsonPropertyName("code")]
    public string? Code { get; init; }
}

/// <summary>
/// VAD status update (optional, sent if client requests).
/// </summary>
public record SttVadStatusMessage : SttWebSocketMessage
{
    public override string Type => SttMessageTypes.VadStatus;
    
    [JsonPropertyName("is_speech")]
    public bool IsSpeech { get; init; }
    
    [JsonPropertyName("energy")]
    public double Energy { get; init; }
    
    [JsonPropertyName("threshold")]
    public double Threshold { get; init; }
}
