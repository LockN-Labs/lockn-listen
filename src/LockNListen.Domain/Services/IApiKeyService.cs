using LockNListen.Domain.Models;

namespace LockNListen.Domain.Services
{
    public interface IApiKeyService
    {
        Task<ApiKey> GenerateApiKeyAsync(string description, HashSet<string> scopes, string createdBy);
        Task<ApiKeyValidationResult> ValidateApiKeyAsync(string apiKey);
        Task<bool> RevokeApiKeyAsync(string apiKeyId);
        Task<bool> IsApiKeyValidAsync(string apiKeyId);
    }
}