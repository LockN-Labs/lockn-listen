# LOC-44 Requirements: Authentication & API Keys

## Requirements Overview

1. **API Key Management**
   - Generate API keys with 256-bit entropy
   - Support key CRUD operations (create, revoke, expire)
   - Store keys in hashed format (Argon2id)
   - Maintain audit log of key usage

2. **Key Validation Middleware**
   - Validate API keys in:
     - HTTP Authorization header (Bearer)
     - WebSocket handshake query param
   - Verify key scope/permissions for each request
   - Block invalid/missing keys with 401/403 responses

3. **Rate Limiting**
   - Sliding window (1 minute window)
   - Configurable rates by key scope:
     - Read: 1000 RPS
     - Write: 100 RPS
     - Admin: 10 RPS
   - Track per-key and per-IP rates
   - Throttle excess requests with 429 responses

4. **Key Scopes**
   - Defined scopes: `read`, `write`, `admin`
   - Scope-based access control:
     - `read`: GET endpoints, stream subscriptions
     - `write`: POST/PUT endpoints
     - `admin`: Key management, system config

5. **Secure Key Storage**
   - Hash keys with Argon2id (memory=64MB, time=4)
   - Store hashes in PostgreSQL
   - Encrypt audit logs at rest
   - Use HSM for key generation if available

6. **Key Rotation**
   - Support graceful rotation with:
     - Primary/secondary key pairs
     - Auto-expiration of old keys (7-day window)
     - Usage monitoring during transition
   - API for rotation status checks

7. **Endpoint Integration**
   - Apply middleware to:
     - All REST endpoints under `/api/*`
     - WebSocket endpoint `/api/classify/live`
   - Preserve existing authentication flows
   - Add `/api/auth/validate` endpoint for key testing

## Dependencies
- ASP.NET Core 8 middleware
- PostgreSQL 15 with pgcrypto
- Redis for rate limiting cache
- Existing WebSocket and REST architectures

## Performance Requirements
- <1ms key validation latency
- 10,000 RPS throughput at 99% percentile
- 0.1% error rate under load

## Security Requirements
- FIPS 140-2 compliant key storage
- Perfect forward secrecy for rotation
- PCI DSS compliant key handling
- SOC 2 Type II audit readiness

## Error Handling
- 401 for missing/invalid keys
- 403 for scope violations
- 429 for rate-limited requests
- 503 for temporary auth service outages
- Detailed audit logging of all failures