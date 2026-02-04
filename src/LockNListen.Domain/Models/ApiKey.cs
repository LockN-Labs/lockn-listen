using System.ComponentModel.DataAnnotations;

namespace LockNListen.Domain.Models
{
    public class ApiKey
    {
        public string Id { get; set; } = string.Empty;

        public string HashedKey { get; set; } = string.Empty;

        public string? Description { get; set; }

        public HashSet<string> Scopes { get; set; } = new HashSet<string>();

        public DateTime CreatedAt { get; set; }

        public DateTime? ExpiresAt { get; set; }

        public bool IsRevoked { get; set; }

        public string CreatedBy { get; set; } = string.Empty;
    }
}