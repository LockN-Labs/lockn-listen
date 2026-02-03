# LOC-41-requirements.md

## API Endpoints

### POST /api/transcribe
**Description:** Transcribe audio file to text using CUDA-accelerated Whisper model

**Request Requirements:**
- Content-Type: multipart/form-data (with "audio" part) or application/octet-stream
- Supported formats: WAV/FLAC/MP3/MP4 with PCM/FLOAT audio
- Max file size: 100MB

**Request Body (multipart/form-data):**
```json
{
  "audio": <binary audio file>
}
```

**Request Body (octet-stream):**
Raw audio file content (must match supported formats)

**Response (200 OK):**
```json
{
  "text": "Transcribed text...",
  "language": "en",
  "duration": 123.45,
  "segments": [
    {
      "start": 0.0,
      "end": 4.5,
      "text": "First segment..."
    }
  ]
}
```

**Error Responses:**
- 400 Bad Request: Unsupported format, invalid audio, or exceeds max size
- 503 Service Unavailable: All processor slots occupied
- 500 Internal Error: Service failure with error details in response body