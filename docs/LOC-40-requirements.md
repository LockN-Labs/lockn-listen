# LOC-40: Whisper.net Integration — Requirements

**Phase 1: Requirements** | Agent: Opus (orchestrator)

## Goal

Integrate Whisper.net (sandrohanea/whisper.net) as the local speech-to-text engine for LockN Listen. This provides on-device transcription without cloud API dependencies, using NVIDIA GPU acceleration via CUDA.

## Acceptance Criteria

- [ ] `WhisperSttService` implements `ISttService` interface
- [ ] Supports Whisper model sizes: tiny, base, small, medium (large optional)
- [ ] GPU acceleration via CUDA runtime (RTX Pro 6000 Blackwell target)
- [ ] Accepts audio streams (WAV, 16kHz mono) and returns transcription
- [ ] Returns word-level timestamps when available
- [ ] Handles language detection or accepts language hint parameter
- [ ] Model files downloaded/cached in configurable path
- [ ] Unit tests for transcription service
- [ ] Integration test with sample audio file

## Technical Scope

### Files to Create/Modify

| Path | Action | Purpose |
|------|--------|---------|
| `src/LockNListen.Infrastructure/Services/WhisperSttService.cs` | Modify | Full implementation |
| `src/LockNListen.Domain/DTOs/TranscriptionResult.cs` | Create | Result DTO with text, segments, language |
| `src/LockNListen.Domain/DTOs/TranscriptionSegment.cs` | Create | Segment with start/end timestamps |
| `src/LockNListen.Infrastructure/LockNListen.Infrastructure.csproj` | Modify | Add Whisper.net packages |
| `tests/LockNListen.Tests/WhisperSttServiceTests.cs` | Create | Unit tests |
| `tests/LockNListen.Tests/samples/` | Create | Test audio files |

### Dependencies

```xml
<PackageReference Include="Whisper.net" Version="1.9.0" />
<PackageReference Include="Whisper.net.Runtime.Cuda" Version="1.9.0" />
```

### Interface Contract

```csharp
public interface ISttService
{
    Task<TranscriptionResult> TranscribeAsync(
        Stream audioStream, 
        string? language = null,
        CancellationToken ct = default);
    
    Task<TranscriptionResult> TranscribeFileAsync(
        string filePath,
        string? language = null,
        CancellationToken ct = default);
    
    IAsyncEnumerable<TranscriptionSegment> TranscribeStreamingAsync(
        Stream audioStream,
        string? language = null,
        CancellationToken ct = default);
}

public record TranscriptionResult(
    string Text,
    string DetectedLanguage,
    TimeSpan Duration,
    List<TranscriptionSegment> Segments);

public record TranscriptionSegment(
    string Text,
    TimeSpan Start,
    TimeSpan End,
    float Confidence);
```

### Configuration

```json
{
  "Whisper": {
    "ModelPath": "./models/whisper",
    "ModelSize": "small",
    "Language": "en",
    "UseGpu": true
  }
}
```

## Constraints

- **License:** Whisper.net is MIT licensed ✅
- **VRAM:** Model sizes vary (tiny ~1GB, small ~2GB, medium ~5GB)
- **Audio format:** Whisper requires 16kHz mono WAV; service must handle resampling
- **Thread safety:** WhisperProcessor is not thread-safe; use pooling or per-request instances

## Out of Scope

- Real-time streaming from microphone (future LOC-41)
- Speaker diarization (future enhancement)
- Custom fine-tuned models

## Dependencies

- LOC-39: Project scaffold ✅ DONE

---

**→ Handing off to Codex for Phase 2: Architecture**
