namespace LockNListen.Api.Errors;

/// <summary>
/// Standard error codes used across all LockN APIs.
/// Codes are prefixed by domain for clarity.
/// </summary>
public static class ErrorCodes
{
    // === Authentication & Authorization (AUTH_*) ===
    public const string AuthMissingKey = "AUTH_MISSING_KEY";
    public const string AuthInvalidKey = "AUTH_INVALID_KEY";
    public const string AuthExpiredKey = "AUTH_EXPIRED_KEY";
    public const string AuthInsufficientPermissions = "AUTH_INSUFFICIENT_PERMISSIONS";
    public const string AuthRateLimitExceeded = "AUTH_RATE_LIMIT_EXCEEDED";
    
    // === Validation (VALIDATION_*) ===
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string ValidationMissingField = "VALIDATION_MISSING_FIELD";
    public const string ValidationInvalidFormat = "VALIDATION_INVALID_FORMAT";
    public const string ValidationOutOfRange = "VALIDATION_OUT_OF_RANGE";
    
    // === Resource (RESOURCE_*) ===
    public const string ResourceNotFound = "RESOURCE_NOT_FOUND";
    public const string ResourceAlreadyExists = "RESOURCE_ALREADY_EXISTS";
    public const string ResourceConflict = "RESOURCE_CONFLICT";
    public const string ResourceGone = "RESOURCE_GONE";
    
    // === External Services (EXTERNAL_*) ===
    public const string ExternalServiceUnavailable = "EXTERNAL_SERVICE_UNAVAILABLE";
    public const string ExternalServiceTimeout = "EXTERNAL_SERVICE_TIMEOUT";
    public const string ExternalServiceError = "EXTERNAL_SERVICE_ERROR";
    
    // === Server (SERVER_*) ===
    public const string ServerInternalError = "SERVER_INTERNAL_ERROR";
    public const string ServerBusy = "SERVER_BUSY";
    public const string ServerMaintenanceMode = "SERVER_MAINTENANCE_MODE";
    
    // === Domain-specific: Listen (LISTEN_*) ===
    public const string ListenTranscriptionFailed = "LISTEN_TRANSCRIPTION_FAILED";
    public const string ListenClassificationFailed = "LISTEN_CLASSIFICATION_FAILED";
    public const string ListenInvalidAudioFormat = "LISTEN_INVALID_AUDIO_FORMAT";
    public const string ListenModelNotLoaded = "LISTEN_MODEL_NOT_LOADED";
    public const string ListenStreamingFailed = "LISTEN_STREAMING_FAILED";
}
