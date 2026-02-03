using LockNListen.Domain.Models;
using LockNListen.Domain.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Security.Cryptography;

namespace LockNListen.Api.Endpoints
{
    public static class AuthEndpoints
    {
        public static void MapAuthEndpoints(this IEndpointRouteBuilder routes)
        {
            var group = routes.MapGroup("/api/auth");
            
            group.MapPost("/generate-key", GenerateApiKey)
                .WithTags("Authentication")
                .WithOpenApi();
        }
        
        private static async Task<IResult> GenerateApiKey(
            string description,
            HashSet<string> scopes,
            string createdBy,
            IApiKeyService apiKeyService)
        {
            try
            {
                var apiKey = await apiKeyService.GenerateApiKeyAsync(description, scopes, createdBy);
                return Results.Ok(apiKey);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Error generating API key: {ex.Message}");
            }
        }
    }
}