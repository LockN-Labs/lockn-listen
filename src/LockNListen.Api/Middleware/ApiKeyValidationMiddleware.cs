using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace LockNListen.Api.Middleware
{
    public class ApiKeyValidationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ApiKeyValidationMiddleware> _logger;
        private readonly HttpClient _httpClient;

        public ApiKeyValidationMiddleware(RequestDelegate next, ILogger<ApiKeyValidationMiddleware> logger, HttpClient httpClient)
        {
            _next = next;
            _logger = logger;
            _httpClient = httpClient;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value ?? "";
            
            // Allow health endpoints to remain unauthenticated
            if (IsHealthEndpoint(path))
            {
                await _next(context);
                return;
            }
            
            // Check for API key in Authorization header or X-Api-Key header
            string? apiKey = GetApiKeyFromHeaders(context.Request.Headers);
            
            if (string.IsNullOrEmpty(apiKey))
            {
                await Reject(context, "API key required");
                return;
            }

            // Call lockn-apikeys service for validation
            var validationResponse = await ValidateApiKeyWithService(apiKey);
            if (!validationResponse.IsValid)
            {
                await Reject(context, validationResponse.Error ?? "Invalid or missing API key");
                return;
            }

            // Add validated key info to request context
            context.Items["ApiKey"] = validationResponse;
            
            await _next(context);
        }

        private bool IsHealthEndpoint(string path)
        {
            return path.StartsWith("/health", StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith("/healthz", StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith("/ready", StringComparison.OrdinalIgnoreCase);
        }

        private string? GetApiKeyFromHeaders(IHeaderDictionary headers)
        {
            // Check Authorization header (Bearer token)
            if (headers.TryGetValue("Authorization", out var authHeader) && 
                !string.IsNullOrEmpty(authHeader) && 
                authHeader.ToString().StartsWith("Bearer "))
            {
                return authHeader.ToString().Substring("Bearer ".Length).Trim();
            }
            
            // Check X-Api-Key header
            if (headers.TryGetValue("X-Api-Key", out var apiKeyHeader) && 
                !string.IsNullOrEmpty(apiKeyHeader))
            {
                return apiKeyHeader.ToString().Trim();
            }
            
            return null;
        }

        private async Task<ValidateKeyResponse> ValidateApiKeyWithService(string apiKey)
        {
            try
            {
                var request = new ValidateKeyRequest(apiKey);
                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var baseUrl = Environment.GetEnvironmentVariable("LockNApiKeys__BaseUrl") 
                    ?? "http://localhost:5000"; // Default fallback
                
                var response = await _httpClient.PostAsync($"{baseUrl}/api/keys/validate", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var validateResponse = JsonSerializer.Deserialize<ValidateKeyResponse>(responseContent);
                    return validateResponse ?? new ValidateKeyResponse { IsValid = false, Error = "Failed to parse validation response" };
                }
                else
                {
                    return new ValidateKeyResponse { IsValid = false, Error = $"Validation service error: {response.StatusCode}" };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating API key with lockn-apikeys service");
                return new ValidateKeyResponse { IsValid = false, Error = "Key validation service unavailable" };
            }
        }

        private static async Task Reject(HttpContext context, string message, int statusCode = 401)
        {
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = message }));
        }
    }

    public record ValidateKeyRequest(string Key);

    public record ValidateKeyResponse
    {
        public bool IsValid { get; init; }
        public string? Error { get; init; }
        public Guid KeyId { get; init; }
        public string OwnerId { get; init; } = "";
        public List<string> Scopes { get; init; } = new();
        public int RateLimitPerMinute { get; init; } = 60;
    }
}