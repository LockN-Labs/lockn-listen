namespace LockNListen.Infrastructure.Auth
{
    public class ApiKeyOptions
    {
        public int HashIterations { get; set; } = 4;
        public int HashMemory { get; set; } = 64 * 1024; // 64 MB
        public int KeyLength { get; set; } = 32; // 256 bits
    }
}