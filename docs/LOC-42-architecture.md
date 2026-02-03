# LOC-42 Architecture: ONNX Sound Classifier Integration

## Component Diagram

```text
[Audio Input] --> [Preprocessing Pipeline] --> [ONNX Model]
                   |
[WebSocket Stream] --> [Real-time Classifier] --> [Event Bus]
                   |
[API Endpoint] --> [Batch Classifier] --> [REST Response]
                   |
[Configuration] --> [Model Loader (Singleton)]
```

## Class Design

```csharp
public interface ISoundClassifier {
  Task<ClassificationResult> ClassifyAsync(Stream audioStream);
  Task<StreamClassification> ClassifyStreamAsync(IAsyncEnumerable<byte[]> audioChunks);
}

public class OnnxSoundClassifier : ISoundClassifier {
  private readonly InferenceSession _session;
  private readonly SoundClassifierOptions _options;

  public OnnxSoundClassifier(SoundClassifierOptions options) {
    _options = options;
    _session = LoadModel(options.ModelPath);
  }

  private InferenceSession LoadModel(string path) {
    // Lazy loading implementation
  }

  public async Task<ClassificationResult> ClassifyAsync(Stream audioStream) {
    var preprocessed = PreprocessAudio(audioStream);
    var results = await RunInference(preprocessed);
    return PostprocessResults(results);
  }
}
```

## Audio Preprocessing Pipeline

1. **Resampling**
   - Convert to 16kHz mono using NAudio.Resample
   - Apply anti-aliasing filter

2. **Windowing**
   - 0.96s Hamming windows with 50% overlap
   - Zero-padding for partial windows

3. **Normalization**
   - [-1, 1] amplitude normalization
   - Per-channel energy normalization

## ONNX Model Loading

```csharp
public class ModelLoader {
  private static InferenceSession _session;
  private static readonly object _lock = new();

  public static InferenceSession GetSession(string modelPath) {
    if (_session == null) {
      lock (_lock) {
        if (_session == null) {
          _session = new InferenceSession(modelPath);
        }
      }
    }
    return _session;
  }
}
```

## API Endpoint Design

```csharp
public static class ClassificationEndpoints {
  public static void MapClassificationEndpoints(this IEndpointRouteBuilder routes) {
    var group = routes.MapGroup("/api/classify");

    group.MapPost("/file", ClassifyFile);
    group.MapPost("/stream", ClassifyStream);
    group.MapWebSocket("/live", HandleLiveClassification);
  }

  private static async Task<ClassificationResponse> ClassifyFile(IFormFile file, ISoundClassifier classifier) {
    // Implementation
  }
}
```

## WebSocket Streaming Design

```csharp
private async Task HandleLiveClassification(WebSocket webSocket, ISoundClassifier classifier) {
  var buffer = new byte[4096];
  
  while (webSocket.State == WebSocketState.Open) {
    var result = await webSocket.ReceiveAsync(buffer, CancellationToken.None);
    
    if (result.MessageType == WebSocketMessageType.Text) {
      var audioChunk = ProcessAudioChunk(buffer);
      var classification = await classifier.ClassifyStreamAsync(audioChunk);
      
      await webSocket.SendAsync(
        Serialize(classification),
        WebSocketMessageType.Text,
        true,
        CancellationToken.None
      );
    }
  }
}
```

## Configuration Schema

```csharp
public class SoundClassifierOptions {
  public string ModelPath { get; set; } = "models/yamnet.onnx";
  public float ConfidenceThreshold { get; set; } = 0.7f;
  public int WindowSizeMs { get; set; } = 1000;
  public bool UseGpu { get; set; } = true;
  public int MaxConcurrentStreams { get; set; } = 5;
  public string[] TargetClasses { get; set; } = {
    "Speech",
    "Music",
    "Doorbell",
    "DogBark",
    "Alarm"
  };
}
```

## Dependencies

- Microsoft.ML.OnnxRuntime.Gpu
- NAudio (for audio processing)
- ASP.NET Core Minimal APIs
- Serilog for structured logging

## Performance Notes

- CUDA memory usage: ~450MB for YAMNet inference
- CPU fallback uses 2-3 threads
- Latency: 80-120ms for full classification pipeline

## Model Requirements

- Input: 16kHz mono PCM
- Window size: 0.96s (matching YAMNet)
- Output: 521 class probabilities (filtered to target classes)

## Error Handling

- Model loading failures: Fallback to default model
- Audio format errors: Return 400 with validation details
- GPU out of memory: Switch to CPU automatically
- Streaming errors: Emit error event over WebSocket
