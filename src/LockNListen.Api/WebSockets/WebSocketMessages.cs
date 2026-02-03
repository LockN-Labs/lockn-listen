using System;

namespace LockNListen.Api.WebSockets
{
    public record WebSocketClassificationMessage
    {
        public string Type { get; init; } = "classification";
        public ClassificationData Data { get; init; } = new();
    }

    public record ClassificationData
    {
        public string Category { get; init; } = string.Empty;
        public float Confidence { get; init; }
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    }
}