using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using LockNListen.Domain.Models;
using LockNListen.Domain.Services;

namespace LockNListen.Infrastructure.Auth
{
    public class ApiKeyService : IApiKeyService
    {
        private readonly IApiKeyRepository _apiKeyRepository;
        private readonly ApiKeyOptions _options;
        private readonly ConcurrentDictionary<string, ApiKey> _inMemoryKeys = new();
        
        public ApiKeyService(IApiKeyRepository apiKeyRepository, ApiKeyOptions options)
        {
            _apiKeyRepository = apiKeyRepository;
            _options = options;
        }
        
        public Task<ApiKey> GenerateApiKeyAsync(string description, HashSet<string> scopes, string createdBy)
        {
            // Generate a random 256-bit key
            byte[] keyBytes = new byte[_options.KeyLength];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(keyBytes);
            }
            
            // Convert to Base64 for storage and use
            string apiKey = Convert.ToBase64String(keyBytes);
            
            // Create a hash of the API key for storage
            string hashedKey = HashApiKey(apiKey);
            
            // Create the API key object
            var newApiKey = new ApiKey
            {
                Id = Guid.NewGuid().ToString(),
                HashedKey = hashedKey,
                Description = description,
                Scopes = scopes,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = createdBy
            };
            
            // Save the API key to in-memory storage for MVP
            _inMemoryKeys[newApiKey.Id] = newApiKey;
            
            return Task.FromResult(newApiKey);
        }
        
        public Task<ApiKeyValidationResult> ValidateApiKeyAsync(string apiKey)
        {
            // Hash the provided API key
            string hashedApiKey = HashApiKey(apiKey);
            
            // For MVP, we'll search our in-memory keys
            foreach (var kvp in _inMemoryKeys)
            {
                var storedKey = kvp.Value;
                
                // Timing-safe comparison of the hashed keys
                if (CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(storedKey.HashedKey),
                    Encoding.UTF8.GetBytes(hashedApiKey)))
                {
                    // Validate that the key is not revoked and not expired
                    if (!storedKey.IsRevoked && 
                        (!storedKey.ExpiresAt.HasValue || storedKey.ExpiresAt > DateTime.UtcNow))
                    {
                        return Task.FromResult(new ApiKeyValidationResult
                        {
                            IsValid = true,
                            ApiKey = storedKey,
                            Scopes = storedKey.Scopes
                        });
                    }
                    else
                    {
                        return Task.FromResult(new ApiKeyValidationResult
                        {
                            IsValid = false,
                            ErrorMessage = "API key is revoked or expired"
                        });
                    }
                }
            }
            
            return Task.FromResult(new ApiKeyValidationResult
            {
                IsValid = false,
                ErrorMessage = "Invalid API key"
            });
        }
        
        public Task<bool> RevokeApiKeyAsync(string apiKeyId)
        {
            if (_inMemoryKeys.TryGetValue(apiKeyId, out var apiKey))
            {
                apiKey.IsRevoked = true;
                _inMemoryKeys[apiKeyId] = apiKey;
                return Task.FromResult(true);
            }
            
            return Task.FromResult(false);
        }
        
        public Task<bool> IsApiKeyValidAsync(string apiKeyId)
        {
            if (_inMemoryKeys.TryGetValue(apiKeyId, out var apiKey))
            {
                return Task.FromResult(!apiKey.IsRevoked && 
                    (!apiKey.ExpiresAt.HasValue || apiKey.ExpiresAt > DateTime.UtcNow));
            }
            
            return Task.FromResult(false);
        }
        
        private string HashApiKey(string apiKey)
        {
            // For MVP, we'll use SHA256 as requested (instead of Argon2id)
            using (var sha256 = SHA256.Create())
            {
                byte[] hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(apiKey));
                return Convert.ToBase64String(hashedBytes);
            }
        }
    }
}