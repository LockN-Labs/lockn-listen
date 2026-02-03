## Functional Requirements
- FR-1: WebSocket endpoint at /api/classify/live
- FR-2: Accept binary audio chunks (16kHz, 16-bit mono PCM)
- FR-3: Return JSON classification events: { category, confidence, timestamp }
- FR-4: Support connection lifecycle events
- FR-5: Handle graceful disconnection

## Non-Functional Requirements
- NFR-1: Support 10+ concurrent connections
- NFR-2: Classification latency under 100ms per chunk
- NFR-3: Memory-efficient streaming (no full audio buffering)

## Message Protocol
- Client → Server: Binary audio frames (960 samples = 60ms @ 16kHz)
- Server → Client: JSON events { type: "classification", data: {...} }

## Error Handling
- Invalid audio format rejection
- Connection timeout handling
- Graceful degradation under load