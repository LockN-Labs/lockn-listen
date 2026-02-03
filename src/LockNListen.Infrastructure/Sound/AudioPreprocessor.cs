using System;
using System.Linq;

namespace LockNListen.Infrastructure.Sound
{
    public class AudioPreprocessor
    {
        // Resample audio to target sample rate (basic implementation)
        public byte[] Resample(byte[] audioData, int originalRate, int targetRate)
        {
            // Simple resampling ratio (needs real implementation for production)
            var ratio = (float)targetRate / originalRate;
            return audioData.Where((sample, index) => index % (int)(1.0 / ratio) == 0)
                           .Take(16000) // 1 second of 16kHz audio
                           .ToArray();
        }

        // Apply Hamming window to audio buffer
        public float[] ApplyWindow(float[] audioBuffer)
        {
            for (int i = 0; i < audioBuffer.Length; i++)
            {
                audioBuffer[i] *= (float)(0.54 - 0.46 * Math.Cos(2 * Math.PI * i / (audioBuffer.Length - 1)));
            }
            return audioBuffer;
        }
    }
}
