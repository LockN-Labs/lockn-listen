using System;
using System.Buffers;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LockNListen.Domain.Interfaces;
using LockNListen.Infrastructure.Speech;
using Microsoft.Extensions.Logging;

namespace LockNListen.Api.WebSockets;

/// <summary>
/// WebSocket handler for real-time speech-to-text streaming with VAD.
/// </summary>
public class SttWebSocketHandler
{
    private const int FrameSizeBytes = 1920; // 960 samples * 2 bytes per sample (16-bit)
    private const int SampleRate = 16000;
    private const int MaxAudioBufferMs = 30000; // 30 second max utterance
    private const int MaxAudioBufferBytes = SampleRate * 2 * MaxAudioBufferMs / 1000;
    
    private readonly IStreamingSttService _sttService;
    private readonly ILogger<SttWebSocketHandler> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public SttWebSocketHandler(
        IStreamingSttService sttService,
        ILogger<SttWebSocketHandler> logger)
    {
        _sttService = sttService;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
    }

    public async Task HandleConnectionAsync(WebSocket webSocket, CancellationToken cancellationToken)
    {
        var sessionId = Guid.NewGuid().ToString("N")[..12];
        _logger.LogInformation("STT WebSocket session started: {SessionId}", sessionId);

        var vad = new VoiceActivityDetector();
        var audioBuffer = new List<byte>();
        var config = new SessionConfig();
        string? currentSegmentId = null;
        var segmentStartTime = DateTime.UtcNow;

        try
        {
            // Send ready message
            await SendMessageAsync(webSocket, new SttReadyMessage
            {
                SessionId = sessionId
            }, cancellationToken);

            var receiveBuffer = new byte[4096];
            var messageBuffer = new List<byte>();

            while (webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var result = await webSocket.ReceiveAsync(
                    new ArraySegment<byte>(receiveBuffer),
                    cancellationToken);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogInformation("STT WebSocket session closing: {SessionId}", sessionId);
                    await webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Closed by client",
                        cancellationToken);
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    // Handle text messages (config)
                    messageBuffer.AddRange(receiveBuffer.AsSpan(0, result.Count).ToArray());
                    
                    if (result.EndOfMessage)
                    {
                        var json = Encoding.UTF8.GetString(messageBuffer.ToArray());
                        messageBuffer.Clear();
                        
                        await HandleTextMessageAsync(webSocket, json, config, vad, cancellationToken);
                    }
                    continue;
                }

                if (result.MessageType == WebSocketMessageType.Binary)
                {
                    // Process audio chunk
                    var chunk = receiveBuffer.AsSpan(0, result.Count);
                    
                    // Validate chunk size (should be 60ms frames = 1920 bytes)
                    if (chunk.Length != FrameSizeBytes)
                    {
                        _logger.LogWarning(
                            "Invalid audio chunk size: {Size} bytes (expected {Expected})",
                            chunk.Length, FrameSizeBytes);
                        continue;
                    }

                    var vadResult = vad.ProcessFrame(chunk);

                    // Send VAD status if requested
                    if (config.SendVadStatus)
                    {
                        await SendMessageAsync(webSocket, new SttVadStatusMessage
                        {
                            IsSpeech = vadResult.IsSpeech,
                            Energy = vadResult.SmoothedEnergy,
                            Threshold = vadResult.Threshold
                        }, cancellationToken);
                    }

                    // Handle speech start
                    if (vadResult.JustStartedSpeaking)
                    {
                        currentSegmentId = Guid.NewGuid().ToString("N")[..8];
                        segmentStartTime = DateTime.UtcNow;
                        audioBuffer.Clear();
                        
                        _logger.LogDebug("Speech started: segment {SegmentId}", currentSegmentId);
                        
                        await SendMessageAsync(webSocket, new SttSpeechStartMessage
                        {
                            SegmentId = currentSegmentId
                        }, cancellationToken);
                    }

                    // Buffer audio during speech
                    if (vadResult.IsSpeech || audioBuffer.Count > 0)
                    {
                        audioBuffer.AddRange(chunk.ToArray());
                        
                        // Check for max buffer size
                        if (audioBuffer.Count > MaxAudioBufferBytes)
                        {
                            _logger.LogWarning(
                                "Audio buffer exceeded max size, forcing transcription: {SegmentId}",
                                currentSegmentId);
                            
                            await ProcessAndSendTranscriptionAsync(
                                webSocket,
                                audioBuffer,
                                currentSegmentId ?? "overflow",
                                segmentStartTime,
                                config,
                                cancellationToken);
                            
                            audioBuffer.Clear();
                            currentSegmentId = null;
                        }
                    }

                    // Handle speech end - transcribe the buffered audio
                    if (vadResult.JustStoppedSpeaking && audioBuffer.Count > 0)
                    {
                        var durationMs = (int)(DateTime.UtcNow - segmentStartTime).TotalMilliseconds;
                        
                        _logger.LogDebug(
                            "Speech ended: segment {SegmentId}, {DurationMs}ms, {Bytes} bytes",
                            currentSegmentId, durationMs, audioBuffer.Count);
                        
                        await SendMessageAsync(webSocket, new SttSpeechEndMessage
                        {
                            SegmentId = currentSegmentId ?? "unknown",
                            DurationMs = durationMs
                        }, cancellationToken);

                        // Transcribe in background to not block audio reception
                        var audioData = audioBuffer.ToArray();
                        var segmentId = currentSegmentId ?? "unknown";
                        
                        _ = ProcessAndSendTranscriptionAsync(
                            webSocket,
                            new List<byte>(audioData),
                            segmentId,
                            segmentStartTime,
                            config,
                            cancellationToken);
                        
                        audioBuffer.Clear();
                        currentSegmentId = null;
                    }
                }
            }
        }
        catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
        {
            _logger.LogInformation("STT WebSocket connection closed prematurely: {SessionId}", sessionId);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("STT WebSocket session cancelled: {SessionId}", sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in STT WebSocket session: {SessionId}", sessionId);
            
            if (webSocket.State == WebSocketState.Open)
            {
                try
                {
                    await SendMessageAsync(webSocket, new SttErrorMessage
                    {
                        Error = "Internal server error",
                        Code = "INTERNAL_ERROR"
                    }, cancellationToken);
                    
                    await webSocket.CloseAsync(
                        WebSocketCloseStatus.InternalServerError,
                        "Internal server error",
                        cancellationToken);
                }
                catch { /* Ignore close errors */ }
            }
        }
        finally
        {
            _logger.LogInformation("STT WebSocket session ended: {SessionId}", sessionId);
        }
    }

    private async Task HandleTextMessageAsync(
        WebSocket webSocket,
        string json,
        SessionConfig config,
        VoiceActivityDetector vad,
        CancellationToken cancellationToken)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            if (root.TryGetProperty("type", out var typeProp) && 
                typeProp.GetString() == SttMessageTypes.Config)
            {
                if (root.TryGetProperty("language", out var langProp))
                {
                    config.Language = langProp.GetString();
                }
                
                if (root.TryGetProperty("send_vad_status", out var vadProp))
                {
                    config.SendVadStatus = vadProp.GetBoolean();
                }
                
                _logger.LogDebug(
                    "Session configured: language={Language}, sendVadStatus={SendVadStatus}",
                    config.Language, config.SendVadStatus);
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse config message");
            await SendMessageAsync(webSocket, new SttErrorMessage
            {
                Error = "Invalid JSON in config message",
                Code = "INVALID_CONFIG"
            }, cancellationToken);
        }
    }

    private async Task ProcessAndSendTranscriptionAsync(
        WebSocket webSocket,
        List<byte> audioBuffer,
        string segmentId,
        DateTime segmentStartTime,
        SessionConfig config,
        CancellationToken cancellationToken)
    {
        try
        {
            var audioData = audioBuffer.ToArray();
            
            // Skip very short segments (< 200ms)
            var durationMs = audioData.Length * 1000 / (SampleRate * 2);
            if (durationMs < 200)
            {
                _logger.LogDebug("Skipping short segment {SegmentId}: {DurationMs}ms", segmentId, durationMs);
                return;
            }

            var result = await _sttService.TranscribeSegmentAsync(
                audioData,
                config.Language,
                cancellationToken);

            if (webSocket.State != WebSocketState.Open)
            {
                return;
            }

            // Only send if we got meaningful text
            if (!string.IsNullOrWhiteSpace(result.Text))
            {
                await SendMessageAsync(webSocket, new SttTranscriptMessage
                {
                    SegmentId = segmentId,
                    Text = result.Text,
                    IsFinal = result.IsFinal,
                    Confidence = result.Confidence,
                    Language = result.Language,
                    DurationSeconds = result.DurationSeconds
                }, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to transcribe segment {SegmentId}", segmentId);
            
            if (webSocket.State == WebSocketState.Open)
            {
                await SendMessageAsync(webSocket, new SttErrorMessage
                {
                    Error = "Transcription failed",
                    Code = "TRANSCRIPTION_ERROR"
                }, cancellationToken);
            }
        }
    }

    private async Task SendMessageAsync<T>(
        WebSocket webSocket,
        T message,
        CancellationToken cancellationToken) where T : SttWebSocketMessage
    {
        if (webSocket.State != WebSocketState.Open) return;

        var json = JsonSerializer.Serialize(message, _jsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        
        await webSocket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            true,
            cancellationToken);
    }

    private class SessionConfig
    {
        public string? Language { get; set; }
        public bool SendVadStatus { get; set; }
    }
}
