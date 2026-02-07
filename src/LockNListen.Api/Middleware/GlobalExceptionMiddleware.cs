using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using LockNListen.Api.Errors;

namespace LockNListen.Api.Middleware;

/// <summary>
/// Global exception handling middleware that converts all exceptions
/// into standardized ErrorResponse JSON format.
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
        
        var (code, message, statusCode, details) = exception switch
        {
            LockNException lockNEx => (
                lockNEx.Code,
                lockNEx.Message,
                lockNEx.StatusCode,
                lockNEx.Details
            ),
            
            OperationCanceledException => (
                ErrorCodes.ServerBusy,
                "Request was cancelled",
                499, // Client Closed Request
                null as Dictionary<string, object>
            ),
            
            TimeoutException => (
                ErrorCodes.ExternalServiceTimeout,
                "Operation timed out",
                504,
                null
            ),
            
            HttpRequestException httpEx => (
                ErrorCodes.ExternalServiceError,
                "External service communication failed",
                502,
                _env.IsDevelopment() 
                    ? new Dictionary<string, object> { ["inner"] = httpEx.Message }
                    : null
            ),
            
            _ => (
                ErrorCodes.ServerInternalError,
                _env.IsDevelopment() ? exception.Message : "An unexpected error occurred",
                500,
                _env.IsDevelopment()
                    ? new Dictionary<string, object> 
                    { 
                        ["type"] = exception.GetType().Name,
                        ["stackTrace"] = exception.StackTrace ?? ""
                    }
                    : null
            )
        };

        // Log the error
        if (statusCode >= 500)
        {
            _logger.LogError(exception, 
                "Unhandled exception: {Code} - {Message} (TraceId: {TraceId})",
                code, message, traceId);
        }
        else
        {
            _logger.LogWarning(
                "Request failed: {Code} - {Message} (TraceId: {TraceId})",
                code, message, traceId);
        }

        var errorResponse = new ErrorResponse
        {
            Code = code,
            Message = message,
            Status = statusCode,
            TraceId = traceId,
            Path = context.Request.Path,
            Details = details
        };

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(errorResponse, JsonOptions));
    }
}

/// <summary>
/// Extension methods for registering the global exception middleware.
/// </summary>
public static class GlobalExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
    {
        return app.UseMiddleware<GlobalExceptionMiddleware>();
    }
}
