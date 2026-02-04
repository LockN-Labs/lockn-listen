using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LockNListen.Api.WebSockets;
using LockNListen.Domain.Models;
using LockNListen.Domain.Services;

namespace LockNListen.Api.WebSockets
{
    public class ClassificationWebSocketHandler : IWebSocketHandler
    {
        private readonly AudioChunkBuffer _buffer;
        private readonly JsonSerializerOptions _jsonOptions;

        /// <summary>
        /// ISoundClassifier must be thread-safe for concurrent connections.
        /// </summary>
        public ClassificationWebSocketHandler()
        {
            _buffer = new AudioChunkBuffer(1); // 1 frame = 960 samples at 16kHz = 60ms
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        public async Task HandleConnectionAsync(WebSocket webSocket, ISoundClassifier classifier)
        {
            try
            {
                // Set up keep-alive
                webSocket.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);

                // Accept the WebSocket connection
                await webSocket.AcceptAsync();

                var buffer = new byte[1024];
                var arraySegment = new ArraySegment<byte>(buffer);

                // Main WebSocket loop
                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(arraySegment, CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, 
                            "Connection closed by client", CancellationToken.None);
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        // Process audio chunk
                        await ProcessAudioChunk(webSocket, result.Array, result.Count, classifier);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log exception if logging is available
                // For now, we'll just close the connection gracefully
                if (webSocket.State == WebSocketState.Open)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.InternalServerError, 
                        "Internal server error", CancellationToken.None);
                }
            }
            finally
            {
                _buffer.Reset();
            }
        }

        private async Task ProcessAudioChunk(WebSocket webSocket, byte[] chunk, int count, ISoundClassifier classifier)
        {
            // Validate PCM format: 16-bit mono
            if (count % 2 != 0)
            {
                // Invalid PCM format - not 16-bit
                await webSocket.CloseAsync(WebSocketCloseStatus.ProtocolError, 
                    "Invalid PCM format", CancellationToken.None);
                return;
            }

            // Validate audio chunk size: exactly 960 samples (1920 bytes for 16-bit PCM)
            if (count != 1920)
            {
                // Reject invalid sizes
                await webSocket.CloseAsync(WebSocketCloseStatus.MessageTooBig, 
                    "Invalid audio chunk size", CancellationToken.None);
                return;
            }

            // Copy only the relevant portion of the chunk
            var actualChunk = new byte[count];
            Array.Copy(chunk, actualChunk, count);
            
            _buffer.Append(actualChunk);
            
            if (_buffer.IsFull)
            {
                using var stream = _buffer.GetStream();
                try
                {
                    var classification = await classifier.ClassifyAsync(stream);
                    await SendClassificationEvent(webSocket, classification);
                }
                finally
                {
                    _buffer.Reset();
                }
            }
        }

        private async Task SendClassificationEvent(WebSocket webSocket, SoundClassification classification)
        {
            var message = new WebSocketClassificationMessage
            {
                Data = new ClassificationData
                {
                    Category = classification.Category,
                    Confidence = classification.Confidence,
                    Timestamp = classification.Timestamp
                }
            };

            var json = JsonSerializer.Serialize(message, _jsonOptions);
            var bytes = Encoding.UTF8.GetBytes(json);
            
            // Send back to client
            var arraySegment = new ArraySegment<byte>(bytes);
            // Check WebSocket state before sending to handle backpressure
            if (webSocket.State == WebSocketState.Open)
            {
                await webSocket.SendAsync(arraySegment, WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }
    }
}