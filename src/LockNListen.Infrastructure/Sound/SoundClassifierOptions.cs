namespace LockNListen.Infrastructure.Sound
{
    public class SoundClassifierOptions
    {
        public string ModelPath { get; set; } = string.Empty;
        public string[] TargetClasses { get; set; } = ["Speech", "Music", "Silence", "Doorbell", "Dog", "Alarm"];
        public float ConfidenceThreshold { get; set; } = 0.7f;
    }
}
