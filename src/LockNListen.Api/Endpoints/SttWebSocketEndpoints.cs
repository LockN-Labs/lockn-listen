using LockNListen.Api.WebSockets;
using LockNListen.Domain.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace LockNListen.Api.Endpoints;

/// <summary>
/// WebSocket endpoints for real-time STT streaming.
/// </summary>
public static class SttWebSocketEndpoints
{
    /// <summary>
    /// Maps the /api/listen/ws WebSocket endpoint for real-time speech-to-text.
    /// </summary>
    public static void MapSttWebSocketEndpoints(this IEndpointRouteBuilder app)
    {
        app.Map("/api/listen/ws", async (
            HttpContext context,
            IStreamingSttService sttService,
            ILogger<SttWebSocketHandler> logger) =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync(
                    "This endpoint requires a WebSocket connection. " +
                    "Send 16kHz 16-bit mono PCM audio in 60ms chunks (1920 bytes each).");
                return;
            }

            using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            var handler = new SttWebSocketHandler(sttService, logger);
            await handler.HandleConnectionAsync(webSocket, context.RequestAborted);
        })
        .WithName("StreamingSTT")
        .WithTags("Speech-to-Text")
        .WithOpenApi(operation =>
        {
            operation.Summary = "Real-time Speech-to-Text WebSocket";
            operation.Description = """
                WebSocket endpoint for real-time speech-to-text transcription with VAD.
                
                ## Connection
                Connect via WebSocket to `/api/listen/ws`.
                
                ## Audio Format
                Send binary messages containing 16kHz, 16-bit, mono PCM audio.
                Each message should be exactly 1920 bytes (60ms of audio).
                
                ## Configuration (optional)
                Send a JSON text message to configure the session:
                ```json
                {
                    "type": "config",
                    "language": "en",
                    "send_vad_status": false
                }
                ```
                
                ## Messages from Server
                
                ### ready
                Sent when connection is established.
                ```json
                {"type": "ready", "session_id": "abc123", "message": "..."}
                ```
                
                ### speech_start
                Sent when speech is detected.
                ```json
                {"type": "speech_start", "segment_id": "xyz789"}
                ```
                
                ### speech_end
                Sent when speech ends, transcription is processing.
                ```json
                {"type": "speech_end", "segment_id": "xyz789", "duration_ms": 2500}
                ```
                
                ### transcript
                Final transcription result.
                ```json
                {
                    "type": "transcript",
                    "segment_id": "xyz789",
                    "text": "Hello world",
                    "is_final": true,
                    "confidence": 0.95,
                    "language": "en",
                    "duration_seconds": 2.5
                }
                ```
                
                ### error
                Error occurred.
                ```json
                {"type": "error", "error": "message", "code": "ERROR_CODE"}
                ```
                
                ### vad_status (if enabled)
                Real-time VAD status for each frame.
                ```json
                {"type": "vad_status", "is_speech": true, "energy": 0.05, "threshold": 0.02}
                ```
                """;
            return operation;
        });
    }
}
