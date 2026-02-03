# LockN Listen

**Audio perception layer for the LockN ecosystem.**

LockN Listen provides auditory awareness capabilities — speech-to-text, sound event detection, and speaker identification — that can be consumed by any LockN service.

## Architecture Position

Listen is a **foundational service**, not part of LockN Voice. Voice consumes Listen for input processing.

```
LockN Voice ──depends on──► LockN Listen
LockN Home  ──depends on──► LockN Listen  
LockN Security ──depends on──► LockN Listen
```

## Features

### MVP
- **Whisper STT** — Local speech-to-text transcription
- **REST API** — Unified audio perception endpoints

### Post-MVP
- Speaker diarization (who said what)
- Sound event detection (doorbell, alarm, etc.)
- Speaker identification (voice fingerprinting)

## Quick Start

```bash
# Build
dotnet build

# Run
dotnet run --project src/LockNListen.Api

# Test transcription
curl -X POST http://localhost:5000/api/transcribe \
  -H "Content-Type: audio/wav" \
  --data-binary @sample.wav
```

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/transcribe` | Transcribe raw audio bytes |
| POST | `/api/transcribe/file` | Transcribe uploaded file |
| GET | `/health` | Health check |

## Tech Stack

- .NET 9
- Whisper.net (local STT)
- ONNX Runtime (sound classification)

## License

Proprietary — OneSun Labs / LockN Labs
