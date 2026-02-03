using LockNListen.Domain.Models;
using LockNListen.Domain.Services;

namespace LockNListen.Infrastructure.Auth
{
    public class InMemoryApiKeyRepository : IApiKeyRepository
    {
        private readonly ConcurrentDictionary<string, ApiKey> _apiKeys = new();
        
        public Task<ApiKey?> GetApiKeyAsync(string apiKeyId)
        {
            if (_apiKeys.TryGetValue(apiKeyId, out var apiKey))
            {
                return Task.FromResult(apiKey);
            }
            return Task.FromResult<ApiKey>(null);
        }
        
        public Task<ApiKey> CreateApiKeyAsync(ApiKey apiKey)
        {
            _apiKeys[apiKey.Id] = apiKey;
            return Task.FromResult(apiKey);
        }
        
        public Task<bool> UpdateApiKeyAsync(ApiKey apiKey)
        {
            _apiKeys[apiKey.Id] = apiKey;
            return Task.FromResult(true);
        }
        
        public Task<bool> DeleteApiKeyAsync(string apiKeyId)
        {
            return Task.FromResult(_apiKeys.TryRemove(apiKeyId, out _));
        }
    }
}