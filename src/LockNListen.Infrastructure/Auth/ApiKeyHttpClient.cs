using System.Text;
using System.Text.Json;
using LockNListen.Domain.Models;
using Microsoft.Extensions.Logging;

namespace LockNListen.Infrastructure.Auth
{
    public class ApiKeyHttpClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ApiKeyHttpClient> _logger;

        public ApiKeyHttpClient(HttpClient httpClient, ILogger<ApiKeyHttpClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<ValidateKeyResponse> ValidateKeyAsync(string apiKey)
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
    }
}