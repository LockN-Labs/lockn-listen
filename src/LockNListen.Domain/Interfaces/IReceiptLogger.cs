using System;
using System.Threading.Tasks;

namespace LockNListen.Domain.Interfaces
{
    public interface IReceiptLogger
    {
        Task LogTranscriptionReceiptAsync(TimeSpan audioDuration, string model, TimeSpan latency, string language);
    }
}