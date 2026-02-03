using System;

namespace LockNListen.Domain.Models
{
    public class SoundClassification
    {
        public string Category { get; set; }
        public float Confidence { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
