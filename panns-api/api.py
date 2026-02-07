"""
PANNs Audio Tagging API for LockN Listen
Sound event detection and classification with standardized error handling
"""

import os
import tempfile
import traceback
import uuid
from datetime import datetime, timezone
from typing import Optional, List, Dict, Any
from fastapi import FastAPI, File, UploadFile, HTTPException, Request
from fastapi.responses import JSONResponse
from pydantic import BaseModel
import numpy as np
import librosa

app = FastAPI(title="LockN Listen - PANNs Audio Tagging", version="0.2.0")


# === Error Handling ===

class ErrorCodes:
    """Standard error codes used across all LockN APIs."""
    # Authentication
    AUTH_MISSING_KEY = "AUTH_MISSING_KEY"
    AUTH_INVALID_KEY = "AUTH_INVALID_KEY"
    AUTH_RATE_LIMIT_EXCEEDED = "AUTH_RATE_LIMIT_EXCEEDED"
    
    # Validation
    VALIDATION_FAILED = "VALIDATION_FAILED"
    VALIDATION_MISSING_FIELD = "VALIDATION_MISSING_FIELD"
    VALIDATION_INVALID_FORMAT = "VALIDATION_INVALID_FORMAT"
    
    # Listen-specific
    LISTEN_CLASSIFICATION_FAILED = "LISTEN_CLASSIFICATION_FAILED"
    LISTEN_INVALID_AUDIO_FORMAT = "LISTEN_INVALID_AUDIO_FORMAT"
    LISTEN_MODEL_NOT_LOADED = "LISTEN_MODEL_NOT_LOADED"
    
    # Server
    SERVER_INTERNAL_ERROR = "SERVER_INTERNAL_ERROR"
    SERVER_BUSY = "SERVER_BUSY"


class ErrorResponse(BaseModel):
    """Standard error response format for all LockN APIs."""
    code: str
    message: str
    status: int
    traceId: Optional[str] = None
    timestamp: str
    path: Optional[str] = None
    details: Optional[Dict[str, Any]] = None


def create_error_response(
    code: str, 
    message: str, 
    status: int,
    path: str = None,
    details: Dict[str, Any] = None
) -> ErrorResponse:
    """Create a standardized error response."""
    return ErrorResponse(
        code=code,
        message=message,
        status=status,
        traceId=str(uuid.uuid4()),
        timestamp=datetime.now(timezone.utc).isoformat(),
        path=path,
        details=details
    )


class LockNHTTPException(HTTPException):
    """Custom HTTP exception with structured error info."""
    def __init__(
        self, 
        code: str, 
        message: str, 
        status_code: int = 500,
        details: Dict[str, Any] = None
    ):
        super().__init__(status_code=status_code, detail=message)
        self.code = code
        self.details = details


@app.exception_handler(LockNHTTPException)
async def lockn_exception_handler(request: Request, exc: LockNHTTPException):
    """Handle LockN-specific exceptions."""
    error = create_error_response(
        code=exc.code,
        message=exc.detail,
        status=exc.status_code,
        path=str(request.url.path),
        details=exc.details
    )
    return JSONResponse(status_code=exc.status_code, content=error.model_dump(exclude_none=True))


@app.exception_handler(HTTPException)
async def http_exception_handler(request: Request, exc: HTTPException):
    """Handle standard HTTP exceptions."""
    error = create_error_response(
        code=ErrorCodes.SERVER_INTERNAL_ERROR,
        message=str(exc.detail),
        status=exc.status_code,
        path=str(request.url.path)
    )
    return JSONResponse(status_code=exc.status_code, content=error.model_dump(exclude_none=True))


@app.exception_handler(Exception)
async def generic_exception_handler(request: Request, exc: Exception):
    """Handle all uncaught exceptions."""
    is_debug = os.getenv("DEBUG", "false").lower() == "true"
    
    error = create_error_response(
        code=ErrorCodes.SERVER_INTERNAL_ERROR,
        message=str(exc) if is_debug else "An unexpected error occurred",
        status=500,
        path=str(request.url.path),
        details={"traceback": traceback.format_exc()} if is_debug else None
    )
    return JSONResponse(status_code=500, content=error.model_dump(exclude_none=True))


# === Model Setup ===

_model = None

def get_model():
    global _model
    if _model is None:
        try:
            from panns_inference import AudioTagging
            device = 'cuda' if os.getenv('PANNS_DEVICE', 'cpu') == 'cuda' else 'cpu'
            _model = AudioTagging(checkpoint_path=None, device=device)
        except Exception as e:
            raise LockNHTTPException(
                code=ErrorCodes.LISTEN_MODEL_NOT_LOADED,
                message=f"Failed to load PANNs model: {str(e)}",
                status_code=503
            )
    return _model


# === Response Models ===

class TagResult(BaseModel):
    label: str
    confidence: float


class ClassifyResponse(BaseModel):
    tags: List[TagResult]
    duration_seconds: float


class EventDetection(BaseModel):
    event: str
    timestamp_ms: float
    confidence: float


class DetectResponse(BaseModel):
    events: List[EventDetection]
    duration_seconds: float


class HealthResponse(BaseModel):
    status: str
    model: str


# Relevant sound classes for sports/ping pong
SPORTS_SOUNDS = {
    'Ping-pong ball': 'bounce',
    'Table tennis': 'game',
    'Bouncing': 'bounce',
    'Knock': 'hit',
    'Tap': 'hit',
    'Clapping': 'point',
    'Cheering': 'point',
    'Speech': 'voice',
    'Silence': 'silence'
}


@app.get("/health")
async def health() -> HealthResponse:
    return HealthResponse(status="healthy", model="PANNs AudioSet")


@app.post("/v1/audio/classify")
async def classify_audio(
    file: UploadFile = File(...),
    top_k: int = 10
) -> ClassifyResponse:
    """Classify sounds in audio file"""
    
    if not file.filename:
        raise LockNHTTPException(
            code=ErrorCodes.VALIDATION_MISSING_FIELD,
            message="No file provided",
            status_code=400,
            details={"field": "file"}
        )
    
    with tempfile.NamedTemporaryFile(delete=False, suffix=".wav") as tmp:
        content = await file.read()
        if len(content) == 0:
            raise LockNHTTPException(
                code=ErrorCodes.LISTEN_INVALID_AUDIO_FORMAT,
                message="Empty audio file",
                status_code=400
            )
        tmp.write(content)
        tmp_path = tmp.name
    
    try:
        # Load audio
        try:
            audio, sr = librosa.load(tmp_path, sr=32000, mono=True)
        except Exception as e:
            raise LockNHTTPException(
                code=ErrorCodes.LISTEN_INVALID_AUDIO_FORMAT,
                message=f"Failed to load audio: {str(e)}",
                status_code=400,
                details={"expectedFormat": "WAV, MP3, or other librosa-compatible format"}
            )
        
        duration = len(audio) / sr
        
        # Run inference
        model = get_model()
        clipwise_output, _ = model.inference(audio[None, :])
        
        # Get top predictions
        labels = model.labels
        probs = clipwise_output[0]
        top_indices = np.argsort(probs)[-top_k:][::-1]
        
        tags = [
            TagResult(label=labels[i], confidence=float(probs[i]))
            for i in top_indices
            if probs[i] > 0.05  # Filter low confidence
        ]
        
        return ClassifyResponse(tags=tags, duration_seconds=duration)
        
    except LockNHTTPException:
        raise
    except Exception as e:
        raise LockNHTTPException(
            code=ErrorCodes.LISTEN_CLASSIFICATION_FAILED,
            message=f"Classification failed: {str(e)}",
            status_code=502,
            details={"originalError": str(e)}
        )
    finally:
        if os.path.exists(tmp_path):
            os.unlink(tmp_path)


@app.post("/v1/audio/detect")
async def detect_events(
    file: UploadFile = File(...),
    events: Optional[List[str]] = None,
    window_ms: int = 500
) -> DetectResponse:
    """Detect specific sound events with timestamps"""
    
    if not file.filename:
        raise LockNHTTPException(
            code=ErrorCodes.VALIDATION_MISSING_FIELD,
            message="No file provided",
            status_code=400,
            details={"field": "file"}
        )
    
    with tempfile.NamedTemporaryFile(delete=False, suffix=".wav") as tmp:
        content = await file.read()
        if len(content) == 0:
            raise LockNHTTPException(
                code=ErrorCodes.LISTEN_INVALID_AUDIO_FORMAT,
                message="Empty audio file",
                status_code=400
            )
        tmp.write(content)
        tmp_path = tmp.name
    
    try:
        # Load audio
        try:
            audio, sr = librosa.load(tmp_path, sr=32000, mono=True)
        except Exception as e:
            raise LockNHTTPException(
                code=ErrorCodes.LISTEN_INVALID_AUDIO_FORMAT,
                message=f"Failed to load audio: {str(e)}",
                status_code=400,
                details={"expectedFormat": "WAV, MP3, or other librosa-compatible format"}
            )
        
        duration = len(audio) / sr
        
        model = get_model()
        labels = model.labels
        
        # Sliding window detection
        window_samples = int(window_ms * sr / 1000)
        hop_samples = window_samples // 2
        
        detected_events = []
        
        for start in range(0, len(audio) - window_samples, hop_samples):
            chunk = audio[start:start + window_samples]
            clipwise_output, _ = model.inference(chunk[None, :])
            probs = clipwise_output[0]
            
            timestamp_ms = (start / sr) * 1000
            
            for i, prob in enumerate(probs):
                label = labels[i]
                # Check if this is a relevant sports sound
                if label in SPORTS_SOUNDS and prob > 0.3:
                    detected_events.append(EventDetection(
                        event=SPORTS_SOUNDS[label],
                        timestamp_ms=timestamp_ms,
                        confidence=float(prob)
                    ))
        
        # Deduplicate nearby events
        deduped = []
        for event in detected_events:
            if not deduped or event.timestamp_ms - deduped[-1].timestamp_ms > window_ms:
                deduped.append(event)
            elif event.confidence > deduped[-1].confidence:
                deduped[-1] = event
        
        return DetectResponse(events=deduped, duration_seconds=duration)
        
    except LockNHTTPException:
        raise
    except Exception as e:
        raise LockNHTTPException(
            code=ErrorCodes.LISTEN_CLASSIFICATION_FAILED,
            message=f"Event detection failed: {str(e)}",
            status_code=502,
            details={"originalError": str(e)}
        )
    finally:
        if os.path.exists(tmp_path):
            os.unlink(tmp_path)


if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8893)
