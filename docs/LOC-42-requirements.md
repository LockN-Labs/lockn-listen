# LOC-42: Sound Event Detection — ONNX Classifier Integration

## Overview

Add audio classification capability to LockN Listen using ONNX-based models. This enables real-time detection of sound events like speech, music, silence, and environmental sounds.

## User Stories

1. **As a developer**, I want to classify audio streams to detect whether they contain speech, music, or silence
2. **As a developer**, I want to detect environmental sounds (doorbell, dog barking, alarm) for smart home integration
3. **As a developer**, I want real-time classification with low latency for responsive applications

## Functional Requirements

### FR-1: ONNX Runtime Integration
- Integrate Microsoft.ML.OnnxRuntime for model inference
- Support CUDA acceleration when available (fallback to CPU)
- Use YAMNet-compatible model for audio event classification

### FR-2: Audio Classification Service
- Create `ISoundClassifier` interface with `ClassifyAsync(Stream audioStream)` method
- Support classification of audio segments (configurable window size, default 1 second)
- Return top-N classifications with confidence scores

### FR-3: Sound Event Categories
Primary detection categories:
- Speech (human voice detected)
- Music (instrumental or vocal music)
- Silence (ambient noise below threshold)
- Environmental sounds (subset of AudioSet ontology):
  - Doorbell
  - Dog bark
  - Alarm/siren
  - Glass breaking
  - Knock/door

### FR-4: REST API Endpoint
- `POST /api/classify` — classify uploaded audio file
- `POST /api/classify/stream` — classify audio from stream
- Response includes: event category, confidence score, timestamp

### FR-5: Streaming Classification
- Support WebSocket endpoint for real-time classification
- Emit events as sound categories are detected
- Configurable detection threshold (default 0.7 confidence)

## Non-Functional Requirements

### NFR-1: Performance
- Classification latency < 100ms per 1-second audio segment
- Support concurrent classification of 10+ streams

### NFR-2: Model Management
- Download YAMNet ONNX model on first run (~14MB)
- Cache model in configurable directory
- Support model version updates without service restart

### NFR-3: Resource Usage
- VRAM usage < 500MB for CUDA inference
- CPU fallback must remain functional (slower inference acceptable)

## Technical Constraints

- Use `Microsoft.ML.OnnxRuntime.Gpu` for CUDA support
- Audio preprocessing must match YAMNet input requirements (16kHz mono, 0.96s windows)
- Must work alongside Whisper.net without CUDA context conflicts

## Acceptance Criteria

1. [ ] `ISoundClassifier` interface implemented with ONNX backend
2. [ ] `POST /api/classify` endpoint returns correct classifications for test audio
3. [ ] Real-time WebSocket classification works with < 100ms latency
4. [ ] CUDA acceleration functional when GPU available
5. [ ] Model auto-downloads on first use
6. [ ] Unit tests cover classification service logic
7. [ ] Integration test validates end-to-end classification

## Dependencies

- LOC-39: Project scaffold (Done)
- LOC-40: Whisper.net integration (Done)
- LOC-41: STT REST API (Done)

## Out of Scope

- Custom model training
- Multi-model ensemble classification
- Audio source separation
