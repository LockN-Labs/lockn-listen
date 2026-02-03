# LOC-41: STT REST API — Architecture

## Overview

Exposes the Whisper.net STT service (LOC-40) via REST API endpoints with proper validation, error handling, and OpenAPI documentation.

## Endpoints

### POST /api/transcribe
- **Content-Type**: `application/octet-stream`
- **Query params**: `language` (optional, e.g., "en", "es")
- **Response**: `TranscriptionResponse` JSON

### POST /api/transcribe/file  
- **Content-Type**: `multipart/form-data`
- **Body**: `file` (audio file)
- **Query params**: `language` (optional)
- **Response**: `TranscriptionResponse` JSON

## Response Schema

```json
{
  "text": "Full transcription text",
  "language": "en",
  "confidence": 0.95,
  "duration": 12.5,
  "segments": [
    {
      "start": 0.0,
      "end": 2.5,
      "text": "Hello world",
      "confidence": 0.98
    }
  ]
}
```

## Error Handling

| Code | Condition |
|------|-----------|
| 400 | File > 100MB, unsupported format |
| 503 | Processor pool exhausted |
| 500 | Transcription failure |

## Supported Formats

- WAV (RIFF header)
- FLAC (fLaC header)
- MP3 (sync word or ID3 tag)
- MP4/M4A (ftyp box)

## Integration

```csharp
// Program.cs
app.MapTranscriptionEndpoints();
```

Extension method in `TranscriptionEndpoints.cs` registers both endpoints with:
- Input validation (size, format)
- Exception handling (503 for pool exhaustion)
- OpenAPI documentation via `.WithOpenApi()`
