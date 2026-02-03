using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LockNListen.Domain.Models;

namespace LockNListen.Domain.Services
{
    public interface ISoundClassifier
    {
        Task<SoundClassification> ClassifyAsync(byte[] audioData, int sampleRate);
        Task<SoundClassification> ClassifyAsync(Stream audioStream, CancellationToken ct = default);
    }
}
