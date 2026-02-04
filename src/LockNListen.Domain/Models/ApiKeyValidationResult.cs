namespace LockNListen.Domain.Models
{
    public class ApiKeyValidationResult
    {
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
        public ApiKey? ApiKey { get; set; }
        public HashSet<string> Scopes { get; set; } = new HashSet<string>();
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