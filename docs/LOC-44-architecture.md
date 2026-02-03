# LOC-44 Architecture: Authentication & API Keys

## Component Diagram

```text
[Incoming Request] --> [ApiKeyMiddleware]
                   |
[ApiKeyMiddleware] --> [ApiKeyService] 
                   |
[ApiKeyService] <-> [InMemoryApiKeyRepository] (MVP)
```

## Class Design

### Domain Layer

```csharp
// Models
public class ApiKey {
    public string Id { get; set; }
    public string HashedKey { get; set; }
    public string Description { get; set; }
    public HashSet<string> Scopes { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
}

public class ApiKeyValidationResult {
    public bool IsValid { get; set; }
    public ApiKey? ApiKey { get; set; }
    public HashSet<string>? Scopes { get; set; }
    public string? ErrorMessage { get; set; }
}

// Interfaces
public interface IApiKeyService {
    Task<ApiKey> GenerateApiKeyAsync(string description, HashSet<string> scopes, string createdBy);
    Task<ApiKeyValidationResult> ValidateApiKeyAsync(string apiKey);
    Task<bool> RevokeApiKeyAsync(string apiKeyId);
    Task<bool> IsApiKeyValidAsync(string apiKeyId);
}

public interface IApiKeyRepository {
    Task<ApiKey?> GetByIdAsync(string id);
    Task<ApiKey?> GetByHashedKeyAsync(string hashedKey);
    Task SaveAsync(ApiKey apiKey);
    Task DeleteAsync(string id);
}
```

### Infrastructure Layer

```csharp
public class ApiKeyService : IApiKeyService {
    // SHA256 hashing for MVP (Argon2id for production)
    // Timing-safe comparison with CryptographicOperations.FixedTimeEquals
    // Key generation with RandomNumberGenerator.GetBytes(32)
}

public class InMemoryApiKeyRepository : IApiKeyRepository {
    // ConcurrentDictionary<string, ApiKey> storage for MVP
}

public class ApiKeyOptions {
    public int KeyLength { get; set; } = 32;
    public string HashAlgorithm { get; set; } = "SHA256";
}
```

### API Layer

```csharp
public class ApiKeyMiddleware {
    // Extract key from Authorization: Bearer header
    // Extract key from api_key query param (WebSocket)
    // Validate and set context items
}

public static class AuthEndpoints {
    // POST /api/auth/keys - Generate new API key (admin only)
}
```

## Middleware Flow

1. Extract API key from `Authorization: Bearer <key>` header
2. Fallback: Extract from `api_key` query parameter (WebSocket handshake)
3. Hash the provided key with SHA256
4. Compare hashes using timing-safe `CryptographicOperations.FixedTimeEquals`
5. Check revocation status and expiration
6. Set `ApiKey` and `Scopes` in `HttpContext.Items`
7. Return 401 if missing/invalid, 403 if revoked

## Security Considerations

- **Hashing**: SHA256 for MVP, upgrade to Argon2id for production
- **Comparison**: Timing-safe to prevent timing attacks
- **Storage**: In-memory for MVP, PostgreSQL with encrypted columns for production
- **Transmission**: Require HTTPS in production

## MVP Scope

Included:
- API key generation with 256-bit entropy
- Key validation middleware
- Bearer token and WebSocket query param auth
- Scope storage (enforcement deferred)
- Key revocation

Deferred to future tickets:
- Rate limiting (LOC-46)
- Audit logging
- Key rotation
- PostgreSQL persistence
- FIPS 140-2 compliance
