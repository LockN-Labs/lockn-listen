"""
Faster-Whisper API for LockN Listen
OpenAI-compatible transcription endpoint with standardized error handling
"""

import os
import tempfile
import traceback
import uuid
from datetime import datetime, timezone
from typing import Optional, Dict, Any
from fastapi import FastAPI, File, UploadFile, Form, HTTPException, Request
from fastapi.responses import JSONResponse
from pydantic import BaseModel
from faster_whisper import WhisperModel
import time

app = FastAPI(title="LockN Listen - Whisper API", version="0.2.0")


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
    LISTEN_TRANSCRIPTION_FAILED = "LISTEN_TRANSCRIPTION_FAILED"
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

MODEL_SIZE = os.getenv("WHISPER_MODEL", "large-v3")
DEVICE = os.getenv("WHISPER_DEVICE", "cuda")
COMPUTE_TYPE = os.getenv("WHISPER_COMPUTE_TYPE", "float16")

model = None

@app.on_event("startup")
async def load_model():
    """Load Whisper model on startup."""
    global model
    print(f"Loading Whisper model: {MODEL_SIZE} on {DEVICE} ({COMPUTE_TYPE})")
    model = WhisperModel(MODEL_SIZE, device=DEVICE, compute_type=COMPUTE_TYPE)
    print("Model loaded successfully")


# === Response Models ===

class TranscriptionResponse(BaseModel):
    text: str
    language: str
    duration: float
    segments: list


class HealthResponse(BaseModel):
    status: str
    model: str
    device: str


# === Endpoints ===

@app.get("/health")
async def health() -> HealthResponse:
    if model is None:
        raise LockNHTTPException(
            code=ErrorCodes.LISTEN_MODEL_NOT_LOADED,
            message="Whisper model is not loaded",
            status_code=503
        )
    return HealthResponse(
        status="healthy",
        model=MODEL_SIZE,
        device=DEVICE
    )


@app.post("/v1/audio/transcriptions")
async def transcribe(
    file: UploadFile = File(...),
    language: Optional[str] = Form(None),
    response_format: Optional[str] = Form("json"),
    timestamp_granularities: Optional[str] = Form("segment")
):
    """OpenAI-compatible transcription endpoint"""
    
    if model is None:
        raise LockNHTTPException(
            code=ErrorCodes.LISTEN_MODEL_NOT_LOADED,
            message="Whisper model is not loaded",
            status_code=503
        )
    
    if not file.filename:
        raise LockNHTTPException(
            code=ErrorCodes.VALIDATION_MISSING_FIELD,
            message="No file provided",
            status_code=400,
            details={"field": "file"}
        )
    
    start_time = time.time()
    
    # Save uploaded file temporarily
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
        # Transcribe
        segments, info = model.transcribe(
            tmp_path,
            language=language,
            beam_size=5,
            word_timestamps=True if timestamp_granularities == "word" else False
        )
        
        # Collect segments
        segment_list = []
        full_text = []
        
        for segment in segments:
            segment_list.append({
                "id": segment.id,
                "start": segment.start,
                "end": segment.end,
                "text": segment.text.strip(),
                "confidence": segment.avg_logprob
            })
            full_text.append(segment.text.strip())
        
        duration = time.time() - start_time
        
        if response_format == "text":
            return " ".join(full_text)
        
        return TranscriptionResponse(
            text=" ".join(full_text),
            language=info.language,
            duration=duration,
            segments=segment_list
        )
        
    except Exception as e:
        raise LockNHTTPException(
            code=ErrorCodes.LISTEN_TRANSCRIPTION_FAILED,
            message=f"Transcription failed: {str(e)}",
            status_code=502,
            details={"originalError": str(e)}
        )
    
    finally:
        # Cleanup temp file
        if os.path.exists(tmp_path):
            os.unlink(tmp_path)


@app.post("/transcribe")
async def transcribe_simple(
    file: UploadFile = File(...),
    language: Optional[str] = Form(None)
):
    """Simple transcription endpoint"""
    return await transcribe(file=file, language=language)


if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8890)
