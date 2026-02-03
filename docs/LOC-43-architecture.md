# LOC-43 Architecture: WebSocket Streaming API

## Component Diagram

```text
[WebSocket Client] --> [ClassificationWebSocketHandler]
                  |
[Audio Buffer] --> [OnnxSoundClassifier (Shared)] --> [Classification Events]
                  |
[ASP.NET Core] --> [WebSocket Middleware] --> [Minimal API Endpoint]
```

## Class Design

```csharp
public interface IWebSocketHandler {
  Task HandleConnectionAsync(WebSocket webSocket, ISoundClassifier classifier);
}

public class ClassificationWebSocketHandler : IWebSocketHandler {
  private readonly AudioChunkBuffer _buffer = new(960);

  public async Task HandleConnectionAsync(WebSocket webSocket, ISoundClassifier classifier) {
    // Lifecycle implementation
  }

  private async Task ProcessAudioChunk(byte[] chunk) {
    _buffer.Append(chunk);
    if (_buffer.IsFull) {
      var results = await classifier.ClassifyAsync(_buffer.GetStream());
      await SendClassificationEvent(results);
    }
  }
}

public class AudioChunkBuffer {
  private const int FrameSize = 960;
  private readonly Queue<byte[]> _chunks = new();

  public void Append(byte[] chunk) { /* ... */ }
  public bool IsFull => /* ... */;
  public Stream GetStream() => /* ... */;
}

public record WebSocketClassificationMessage {
  public string Type { get; init; } = "classification";
  public ClassificationData Data { get; init; }
}

public record ClassificationData {
  public string Category { get; init; }
  public float Confidence { get; init; }
  public DateTime Timestamp { get; init; }
}
```

## WebSocket Lifecycle

1. **Handshake**
   - Use `WebSocket.AcceptWebSocketRequestAsync`
   - Validate origin if needed

2. **Binary Handling**
   - 960-sample frames (60ms @ 16kHz)
   - Validate PCM format (16-bit mono)

3. **Response Flow**
   - JSON serialization with System.Text.Json
   - Backpressure handling with `WebSocket.FlushAsync`

4. **Closure**
   - Clean shutdown with 1000 status code
   - Error handling with 1011/1003 codes

## Integration with OnnxSoundClassifier

- Reuse existing `ISoundClassifier`
- Buffer until 0.96s window (960×16 samples)
- Async classification pipeline:
  ```csharp
  var buffer = new AudioChunkBuffer();
  while (webSocket.State == WebSocketState.Open) {
    var result = await webSocket.ReceiveAsync(...);
    buffer.Append(result.Buffer);
    
    if (buffer.IsFull) {
      var classification = await classifier.ClassifyAsync(buffer.GetStream());
      await SendAsync(classification);
      buffer.Reset();
    }
  }
  ```

## Concurrency Model

- **Handler Isolation**: One handler per connection
- **Classifier Sharing**: Singleton `ISoundClassifier`
- **Thread Safety**: Use `ValueTask` for async operations
- **Cancellation**: Propagate `CancellationToken` for all operations

## ASP.NET Core Integration

```csharp
public static class WebSocketEndpoints {
  public static void MapWebSocketEndpoints(this IEndpointRouteBuilder routes) {
    routes.WebSockets.MapWebSocketHandler("/api/classify/live", async (webSocket, classifier) => {
      var handler = new ClassificationWebSocketHandler();
      await handler.HandleConnectionAsync(webSocket, classifier);
    });
  }
}

// In Program.cs:
app.UseWebSockets();
app.MapWebSocketEndpoints();
```

## Performance Considerations

- Use `ArrayPool<byte>` for audio buffers
- Avoid allocations in hot paths
- Use `Memory<T>`/`Span<T>` for zero-copy processing
- Configure `WebSocketOptions.KeepAliveInterval = TimeSpan.FromSeconds(30)`

## Error Handling Strategy

- **Format Errors**: Send 1003 status code
- **Model Errors**: Log and return empty results
- **Timeouts**: Use `ReceiveAsync`/`SendAsync` timeouts
- **Backpressure**: Monitor `WebSocket.State` before sending

