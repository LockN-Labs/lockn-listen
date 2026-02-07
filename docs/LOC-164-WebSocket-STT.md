# LOC-164: Real-time WebSocket STT

## Overview

Real-time speech-to-text transcription via WebSocket with Voice Activity Detection (VAD).

## Endpoint

```
WS /api/listen/ws
```

## Audio Format

- **Sample Rate:** 16kHz
- **Bit Depth:** 16-bit signed
- **Channels:** Mono
- **Frame Size:** 960 samples (60ms) = 1920 bytes per message

## Protocol

### Connection Flow

1. Client connects to `/api/listen/ws`
2. Server sends `ready` message
3. Client optionally sends `config` message
4. Client streams audio as binary WebSocket messages
5. Server detects speech via VAD and sends events:
   - `speech_start` when speech begins
   - `speech_end` when speech ends
   - `transcript` with the transcription result
6. Client can continue streaming or close connection

### Message Types

#### Client → Server

**Binary (Audio)**
```
[1920 bytes of 16-bit PCM audio]
```

**Config (JSON)**
```json
{
  "type": "config",
  "language": "en",
  "send_vad_status": false
}
```

#### Server → Client

**ready**
```json
{
  "type": "ready",
  "session_id": "abc123def456",
  "timestamp": "2024-02-07T08:00:00Z",
  "message": "Ready to receive audio. Send 16kHz 16-bit mono PCM."
}
```

**speech_start**
```json
{
  "type": "speech_start",
  "segment_id": "seg12345",
  "timestamp": "2024-02-07T08:00:01Z"
}
```

**speech_end**
```json
{
  "type": "speech_end",
  "segment_id": "seg12345",
  "duration_ms": 2500,
  "timestamp": "2024-02-07T08:00:03Z"
}
```

**transcript**
```json
{
  "type": "transcript",
  "segment_id": "seg12345",
  "text": "Hello world",
  "is_final": true,
  "confidence": 0.95,
  "language": "en",
  "duration_seconds": 2.5,
  "timestamp": "2024-02-07T08:00:04Z"
}
```

**vad_status** (if enabled)
```json
{
  "type": "vad_status",
  "is_speech": true,
  "energy": 0.05,
  "threshold": 0.02,
  "timestamp": "2024-02-07T08:00:01Z"
}
```

**error**
```json
{
  "type": "error",
  "error": "Transcription failed",
  "code": "TRANSCRIPTION_ERROR",
  "timestamp": "2024-02-07T08:00:01Z"
}
```

## VAD Configuration

The Voice Activity Detector uses energy-based detection with these defaults:

| Parameter | Default | Description |
|-----------|---------|-------------|
| MinEnergyThreshold | 0.01 | Minimum RMS energy to consider as speech |
| NoiseFloorMultiplier | 2.5 | Multiplier over noise floor for speech detection |
| SpeechStartFrames | 3 | Consecutive speech frames to start utterance (~180ms) |
| SilenceEndFrames | 15 | Consecutive silence frames to end utterance (~900ms) |

## Architecture

```
┌─────────────────┐    ┌────────────────────┐    ┌─────────────────┐
│  Client         │    │  lockn-listen      │    │  faster-whisper │
│  (WebSocket)    │───▶│  WebSocket Handler │───▶│  HTTP API       │
│                 │    │  + VAD             │    │  (port 8890)    │
└─────────────────┘    └────────────────────┘    └─────────────────┘
```

1. Client streams audio over WebSocket
2. VAD detects speech boundaries
3. When speech ends, buffered audio sent to faster-whisper
4. Transcription result returned via WebSocket

## Configuration

### appsettings.json

```json
{
  "FasterWhisper": {
    "BaseUrl": "http://localhost:8890",
    "TimeoutSeconds": 30
  }
}
```

### Environment Variables

- `FasterWhisper__BaseUrl` - Override base URL for faster-whisper API

## Example Client (JavaScript)

```javascript
const ws = new WebSocket('ws://localhost:5000/api/listen/ws');

ws.onopen = () => {
  // Optional: configure session
  ws.send(JSON.stringify({
    type: 'config',
    language: 'en',
    send_vad_status: false
  }));
};

ws.onmessage = (event) => {
  const msg = JSON.parse(event.data);
  switch (msg.type) {
    case 'ready':
      console.log('Session:', msg.session_id);
      startAudioCapture();
      break;
    case 'speech_start':
      console.log('Speaking...');
      break;
    case 'transcript':
      console.log('Transcript:', msg.text);
      break;
    case 'error':
      console.error('Error:', msg.error);
      break;
  }
};

async function startAudioCapture() {
  const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
  const audioContext = new AudioContext({ sampleRate: 16000 });
  const source = audioContext.createMediaStreamSource(stream);
  const processor = audioContext.createScriptProcessor(960, 1, 1);
  
  processor.onaudioprocess = (e) => {
    const samples = e.inputBuffer.getChannelData(0);
    const pcm = new Int16Array(samples.length);
    for (let i = 0; i < samples.length; i++) {
      pcm[i] = Math.max(-32768, Math.min(32767, samples[i] * 32768));
    }
    if (ws.readyState === WebSocket.OPEN) {
      ws.send(pcm.buffer);
    }
  };
  
  source.connect(processor);
  processor.connect(audioContext.destination);
}
```

## Testing

```bash
# Run tests
dotnet test tests/LockNListen.Tests

# Test with wscat
wscat -c ws://localhost:5000/api/listen/ws
```

## Dependencies

- faster-whisper running on port 8890
- GPU recommended for low-latency transcription
