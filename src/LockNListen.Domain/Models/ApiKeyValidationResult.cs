namespace LockNListen.Domain.Models
{
    public class ApiKeyValidationResult
    {
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
        public ApiKey? ApiKey { get; set; }
        public HashSet<string> Scopes { get; set; } = new HashSet<string>();
    }
}