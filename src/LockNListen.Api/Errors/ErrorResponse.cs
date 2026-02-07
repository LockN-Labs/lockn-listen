namespace LockNListen.Api.Errors;

/// <summary>
/// Standard error response format for all LockN APIs.
/// Follows RFC 7807 Problem Details pattern with extensions.
/// </summary>
public class ErrorResponse
{
    /// <summary>
    /// Machine-readable error code (e.g., "VALIDATION_ERROR", "NOT_FOUND").
    /// </summary>
    public required string Code { get; init; }
    
    /// <summary>
    /// Human-readable error message.
    /// </summary>
    public required string Message { get; init; }
    
    /// <summary>
    /// HTTP status code.
    /// </summary>
    public int Status { get; init; }
    
    /// <summary>
    /// Unique request identifier for correlation.
    /// </summary>
    public string? TraceId { get; init; }
    
    /// <summary>
    /// Timestamp when the error occurred (ISO 8601).
    /// </summary>
    public string Timestamp { get; init; } = DateTime.UtcNow.ToString("o");
    
    /// <summary>
    /// Request path that caused the error.
    /// </summary>
    public string? Path { get; init; }
    
    /// <summary>
    /// Additional error details (e.g., validation errors by field).
    /// </summary>
    public Dictionary<string, object>? Details { get; init; }
}
