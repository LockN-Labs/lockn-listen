# LockN API Error Handling Standard

All LockN APIs use a consistent error response format inspired by [RFC 7807 Problem Details](https://datatracker.ietf.org/doc/html/rfc7807).

## Error Response Format

```json
{
  "code": "VALIDATION_FAILED",
  "message": "Validation failed for field 'email'",
  "status": 400,
  "traceId": "00-abc123...-01",
  "timestamp": "2026-02-07T13:45:00.000Z",
  "path": "/api/receipts",
  "details": {
    "fields": {
      "email": ["Invalid email format"]
    }
  }
}
```

### Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `code` | string | ✅ | Machine-readable error code (see below) |
| `message` | string | ✅ | Human-readable error description |
| `status` | number | ✅ | HTTP status code |
| `traceId` | string | ❌ | Unique request identifier for correlation |
| `timestamp` | string | ❌ | ISO 8601 timestamp when error occurred |
| `path` | string | ❌ | Request path that caused the error |
| `details` | object | ❌ | Additional context (e.g., validation errors) |

## Standard Error Codes

### Authentication & Authorization (`AUTH_*`)

| Code | Status | Description |
|------|--------|-------------|
| `AUTH_MISSING_KEY` | 401 | API key not provided |
| `AUTH_INVALID_KEY` | 401 | API key is invalid or expired |
| `AUTH_EXPIRED_KEY` | 401 | API key has expired |
| `AUTH_INSUFFICIENT_PERMISSIONS` | 403 | Key lacks required permissions |
| `AUTH_RATE_LIMIT_EXCEEDED` | 429 | Rate limit exceeded |

### Validation (`VALIDATION_*`)

| Code | Status | Description |
|------|--------|-------------|
| `VALIDATION_FAILED` | 400 | General validation failure |
| `VALIDATION_MISSING_FIELD` | 400 | Required field is missing |
| `VALIDATION_INVALID_FORMAT` | 400 | Field format is invalid |
| `VALIDATION_OUT_OF_RANGE` | 400 | Value is outside allowed range |

### Resource (`RESOURCE_*`)

| Code | Status | Description |
|------|--------|-------------|
| `RESOURCE_NOT_FOUND` | 404 | Requested resource doesn't exist |
| `RESOURCE_ALREADY_EXISTS` | 409 | Resource with same identifier exists |
| `RESOURCE_CONFLICT` | 409 | Resource state conflict |
| `RESOURCE_GONE` | 410 | Resource has been deleted |

### External Services (`EXTERNAL_*`)

| Code | Status | Description |
|------|--------|-------------|
| `EXTERNAL_SERVICE_UNAVAILABLE` | 503 | Dependency service is down |
| `EXTERNAL_SERVICE_TIMEOUT` | 504 | Dependency service timed out |
| `EXTERNAL_SERVICE_ERROR` | 502 | Dependency returned an error |

### Server (`SERVER_*`)

| Code | Status | Description |
|------|--------|-------------|
| `SERVER_INTERNAL_ERROR` | 500 | Unexpected server error |
| `SERVER_BUSY` | 503 | Server is temporarily overloaded |
| `SERVER_MAINTENANCE_MODE` | 503 | Server is in maintenance mode |

### Domain-Specific: Listen (`LISTEN_*`)

| Code | Status | Description |
|------|--------|-------------|
| `LISTEN_TRANSCRIPTION_FAILED` | 502 | Speech-to-text failed |
| `LISTEN_CLASSIFICATION_FAILED` | 502 | Audio classification failed |
| `LISTEN_INVALID_AUDIO_FORMAT` | 400 | Unsupported audio format |
| `LISTEN_MODEL_NOT_LOADED` | 503 | Model not loaded/ready |
| `LISTEN_STREAMING_FAILED` | 502 | Streaming STT failed |

## Usage Examples

### Handling Errors in Clients

```typescript
interface ErrorResponse {
  code: string;
  message: string;
  status: number;
  traceId?: string;
  timestamp?: string;
  path?: string;
  details?: Record<string, unknown>;
}

async function callApi<T>(url: string): Promise<T> {
  const response = await fetch(url);
  
  if (!response.ok) {
    const error: ErrorResponse = await response.json();
    
    switch (error.code) {
      case 'AUTH_RATE_LIMIT_EXCEEDED':
        // Wait and retry
        break;
      case 'RESOURCE_NOT_FOUND':
        // Handle missing resource
        break;
      default:
        console.error(`Error ${error.code}: ${error.message}`);
    }
    
    throw new Error(`${error.code}: ${error.message}`);
  }
  
  return response.json();
}
```

### Throwing Errors in .NET

```csharp
using LockNLogger.Api.Errors;

// Simple exceptions
throw LockNException.NotFound(ErrorCodes.ResourceNotFound, "Receipt not found");
throw LockNException.BadRequest(ErrorCodes.ValidationFailed, "Invalid date range");

// Validation with field details
throw new ValidationException("startDate", "Must be before endDate");

// Resource not found
throw new NotFoundException("Receipt", receiptId);
```

### Throwing Errors in Python

```python
from api import LockNHTTPException, ErrorCodes

# Simple error
raise LockNHTTPException(
    code=ErrorCodes.VALIDATION_MISSING_FIELD,
    message="File is required",
    status_code=400,
    details={"field": "file"}
)

# Processing error
raise LockNHTTPException(
    code=ErrorCodes.LISTEN_TRANSCRIPTION_FAILED,
    message="Transcription failed",
    status_code=502,
    details={"originalError": str(e)}
)
```

## HTTP Status Code Mapping

| Status | Usage |
|--------|-------|
| 400 | Validation errors, bad input |
| 401 | Authentication required/failed |
| 403 | Authorization failed (valid auth, no permission) |
| 404 | Resource not found |
| 409 | Conflict (duplicate, state mismatch) |
| 429 | Rate limiting |
| 499 | Client closed request (cancelled) |
| 500 | Unexpected server error |
| 502 | Upstream service error |
| 503 | Service unavailable |
| 504 | Gateway timeout |
