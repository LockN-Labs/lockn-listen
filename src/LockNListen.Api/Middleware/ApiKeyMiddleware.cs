using LockNListen.Domain.Models;
using LockNListen.Domain.Services;

namespace LockNListen.Api.Middleware
{
    public class ApiKeyMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IApiKeyService _apiKeyService;

        public ApiKeyMiddleware(RequestDelegate next, IApiKeyService apiKeyService)
        {
            _next = next;
            _apiKeyService = apiKeyService;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Check for API key in Authorization header
            string? authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            string? apiKey = null;

            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
            {
                apiKey = authHeader.Substring("Bearer ".Length).Trim();
            }

            // If no API key in header, check query parameters for WebSocket
            if (string.IsNullOrEmpty(apiKey) && context.Request.Query.ContainsKey("api_key"))
            {
                apiKey = context.Request.Query["api_key"];
            }

            // If we have an API key, validate it
            if (!string.IsNullOrEmpty(apiKey))
            {
                var validationResult = await _apiKeyService.ValidateApiKeyAsync(apiKey);

                if (!validationResult.IsValid)
                {
                    context.Response.StatusCode = 401; // Unauthorized
                    await context.Response.WriteAsync("Invalid or missing API key");
                    return;
                }

                // Add the API key information to the request context
                context.Items["ApiKey"] = validationResult.ApiKey;
                context.Items["Scopes"] = validationResult.Scopes;
            }
            else
            {
                // No API key provided - for public endpoints this might be okay
                // But for protected endpoints, we should reject
                context.Response.StatusCode = 401; // Unauthorized
                await context.Response.WriteAsync("API key required");
                return;
            }

            await _next(context);
        }
    }
}