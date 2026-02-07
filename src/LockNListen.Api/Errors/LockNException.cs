namespace LockNListen.Api.Errors;

/// <summary>
/// Base exception for all LockN application errors.
/// Carries structured error information for consistent API responses.
/// </summary>
public class LockNException : Exception
{
    public string Code { get; }
    public int StatusCode { get; }
    public Dictionary<string, object>? Details { get; }

    public LockNException(
        string code, 
        string message, 
        int statusCode = 500,
        Dictionary<string, object>? details = null,
        Exception? inner = null) 
        : base(message, inner)
    {
        Code = code;
        StatusCode = statusCode;
        Details = details;
    }

    /// <summary>
    /// Create a 400 Bad Request exception.
    /// </summary>
    public static LockNException BadRequest(string code, string message, Dictionary<string, object>? details = null)
        => new(code, message, 400, details);

    /// <summary>
    /// Create a 401 Unauthorized exception.
    /// </summary>
    public static LockNException Unauthorized(string code, string message)
        => new(code, message, 401);

    /// <summary>
    /// Create a 403 Forbidden exception.
    /// </summary>
    public static LockNException Forbidden(string code, string message)
        => new(code, message, 403);

    /// <summary>
    /// Create a 404 Not Found exception.
    /// </summary>
    public static LockNException NotFound(string code, string message)
        => new(code, message, 404);

    /// <summary>
    /// Create a 409 Conflict exception.
    /// </summary>
    public static LockNException Conflict(string code, string message)
        => new(code, message, 409);

    /// <summary>
    /// Create a 429 Too Many Requests exception.
    /// </summary>
    public static LockNException RateLimited(string message = "Rate limit exceeded")
        => new(ErrorCodes.AuthRateLimitExceeded, message, 429);

    /// <summary>
    /// Create a 503 Service Unavailable exception.
    /// </summary>
    public static LockNException ServiceUnavailable(string code, string message)
        => new(code, message, 503);
}

/// <summary>
/// Validation-specific exception with field-level details.
/// </summary>
public class ValidationException : LockNException
{
    public ValidationException(string message, Dictionary<string, string[]> fieldErrors)
        : base(
            ErrorCodes.ValidationFailed, 
            message, 
            400, 
            new Dictionary<string, object> { ["fields"] = fieldErrors })
    {
    }

    public ValidationException(string field, string error)
        : this($"Validation failed for field '{field}'", 
            new Dictionary<string, string[]> { [field] = [error] })
    {
    }
}

/// <summary>
/// Resource not found exception.
/// </summary>
public class NotFoundException : LockNException
{
    public NotFoundException(string resourceType, object resourceId)
        : base(
            ErrorCodes.ResourceNotFound,
            $"{resourceType} with ID '{resourceId}' not found",
            404,
            new Dictionary<string, object> 
            { 
                ["resourceType"] = resourceType, 
                ["resourceId"] = resourceId.ToString() ?? "" 
            })
    {
    }
}

/// <summary>
/// Transcription failed exception.
/// </summary>
public class TranscriptionException : LockNException
{
    public TranscriptionException(string message, Exception? inner = null)
        : base(
            ErrorCodes.ListenTranscriptionFailed,
            message,
            502,
            inner: inner)
    {
    }
}

/// <summary>
/// Audio classification failed exception.
/// </summary>
public class ClassificationException : LockNException
{
    public ClassificationException(string message, Exception? inner = null)
        : base(
            ErrorCodes.ListenClassificationFailed,
            message,
            502,
            inner: inner)
    {
    }
}

/// <summary>
/// Invalid audio format exception.
/// </summary>
public class InvalidAudioFormatException : LockNException
{
    public InvalidAudioFormatException(string message, string? expectedFormat = null)
        : base(
            ErrorCodes.ListenInvalidAudioFormat,
            message,
            400,
            expectedFormat != null 
                ? new Dictionary<string, object> { ["expectedFormat"] = expectedFormat }
                : null)
    {
    }
}

/// <summary>
/// External service error exception.
/// </summary>
public class ExternalServiceException : LockNException
{
    public ExternalServiceException(string serviceName, string message, Exception? inner = null)
        : base(
            ErrorCodes.ExternalServiceError,
            $"External service '{serviceName}' error: {message}",
            502,
            new Dictionary<string, object> { ["service"] = serviceName },
            inner)
    {
    }
}
