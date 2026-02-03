using LockNListen.Domain.Models;

namespace LockNListen.Domain.Services
{
    public interface IApiKeyRepository
    {
        Task<ApiKey?> GetApiKeyAsync(string apiKeyId);
        Task<ApiKey> CreateApiKeyAsync(ApiKey apiKey);
        Task<bool> UpdateApiKeyAsync(ApiKey apiKey);
        Task<bool> DeleteApiKeyAsync(string apiKeyId);
    }
}