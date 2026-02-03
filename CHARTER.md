# LockN Listen — Charter

## Identity

**LockN Listen** is a foundational **audio perception layer** for the LockN ecosystem. It provides auditory awareness capabilities that can be consumed by any LockN service.

Listen is **not** part of the Voice stack — it's a standalone perception service that Voice (and other services) depend on.

## Core Capabilities

### Speech-to-Text (STT)
- Real-time transcription
- Multi-language support
- Speaker diarization (who said what)

### Environmental Audio Recognition
- Sound event detection (doorbell, alarm, glass break, etc.)
- Ambient noise classification
- Audio scene analysis

### Speaker Identification
- Voice fingerprinting
- Known speaker recognition
- Enrollment and verification

### Audio Analysis
- Music recognition
- Acoustic anomaly detection
- Audio quality assessment

## Architecture Position

```
┌─────────────────────────────────────────────────┐
│              LockN Ecosystem                    │
├─────────────────────────────────────────────────┤
│                                                 │
│   ┌─────────────┐    ┌─────────────────────┐   │
│   │ LockN Voice │    │ Other LockN Services│   │
│   │   (TTS +    │    │  (Home, Security,   │   │
│   │ Conversation│    │   Monitoring, etc.) │   │
│   └──────┬──────┘    └──────────┬──────────┘   │
│          │                      │              │
│          └──────────┬───────────┘              │
│                     ▼                          │
│          ┌─────────────────────┐               │
│          │    LockN Listen     │               │
│          │  (Audio Perception) │               │
│          └─────────────────────┘               │
│                                                 │
└─────────────────────────────────────────────────┘
```

## Dependency Graph

| Service | Depends On Listen For |
|---------|----------------------|
| LockN Voice | STT input, speaker identification |
| LockN Home (future) | Doorbell detection, baby monitor |
| LockN Security (future) | Glass break, alarm detection |

## Tech Stack (Proposed)

- **Runtime:** .NET 9 (aligned with Logger/Voice)
- **STT Engine:** Whisper (local) or cloud fallback
- **Audio Processing:** NAudio / PortAudio bindings
- **ML Inference:** ONNX Runtime for sound classification models

## MVP Scope

1. **Whisper STT integration** — local transcription
2. **Basic speaker diarization** — segment audio by speaker
3. **Sound event detection** — configurable event triggers
4. **REST API** — unified audio perception endpoints

## Non-Goals (MVP)

- Real-time streaming (post-MVP)
- Multi-device audio fusion
- Wake word detection (handled at edge)

## Success Metrics

- STT accuracy: >95% WER on clean audio
- Latency: <500ms for 10s audio clip
- Sound event detection: >90% precision on trained categories

---

*Created: 2026-02-03*
*Status: Charter*
