using LockNListen.Infrastructure.Speech;

namespace LockNListen.Tests;

public class VoiceActivityDetectorTests
{
    [Fact]
    public void ProcessFrame_SilentAudio_ReturnsNoSpeech()
    {
        // Arrange
        var vad = new VoiceActivityDetector();
        var silentFrame = new byte[1920]; // All zeros = silence
        
        // Act - process multiple frames to get past calibration
        VadResult result = default!;
        for (int i = 0; i < 60; i++) // ~3.6s of silence
        {
            result = vad.ProcessFrame(silentFrame);
        }
        
        // Assert
        Assert.False(result.IsSpeech);
        Assert.False(result.JustStartedSpeaking);
        Assert.False(result.JustStoppedSpeaking);
    }

    [Fact]
    public void ProcessFrame_LoudAudio_DetectsSpeech()
    {
        // Arrange
        var vad = new VoiceActivityDetector();
        
        // Create loud audio frame (sine wave at max amplitude)
        var loudFrame = CreateSineWaveFrame(1000, 0.5);
        
        // Act - process enough frames to trigger speech start
        // First, establish noise floor with silence
        var silentFrame = new byte[1920];
        for (int i = 0; i < 60; i++)
        {
            vad.ProcessFrame(silentFrame);
        }
        
        // Now process loud frames
        VadResult? speechStartResult = null;
        for (int i = 0; i < 10; i++)
        {
            var result = vad.ProcessFrame(loudFrame);
            if (result.JustStartedSpeaking)
            {
                speechStartResult = result;
                break;
            }
        }
        
        // Assert
        Assert.NotNull(speechStartResult);
        Assert.True(speechStartResult.IsSpeech);
        Assert.True(speechStartResult.JustStartedSpeaking);
    }

    [Fact]
    public void ProcessFrame_SpeechThenSilence_DetectsSpeechEnd()
    {
        // Arrange
        var vad = new VoiceActivityDetector(new VadOptions
        {
            SilenceEndFrames = 5 // Reduce for faster test
        });
        
        var loudFrame = CreateSineWaveFrame(1000, 0.5);
        var silentFrame = new byte[1920];
        
        // Establish noise floor
        for (int i = 0; i < 60; i++)
        {
            vad.ProcessFrame(silentFrame);
        }
        
        // Start speech
        for (int i = 0; i < 10; i++)
        {
            vad.ProcessFrame(loudFrame);
        }
        
        Assert.True(vad.IsSpeaking);
        
        // Act - transition to silence
        VadResult? speechEndResult = null;
        for (int i = 0; i < 20; i++)
        {
            var result = vad.ProcessFrame(silentFrame);
            if (result.JustStoppedSpeaking)
            {
                speechEndResult = result;
                break;
            }
        }
        
        // Assert
        Assert.NotNull(speechEndResult);
        Assert.False(speechEndResult.IsSpeech);
        Assert.True(speechEndResult.JustStoppedSpeaking);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        // Arrange
        var vad = new VoiceActivityDetector();
        var loudFrame = CreateSineWaveFrame(1000, 0.5);
        
        // Get into speaking state
        for (int i = 0; i < 60; i++)
        {
            vad.ProcessFrame(new byte[1920]);
        }
        for (int i = 0; i < 10; i++)
        {
            vad.ProcessFrame(loudFrame);
        }
        
        Assert.True(vad.IsSpeaking);
        
        // Act
        vad.Reset();
        
        // Assert
        Assert.False(vad.IsSpeaking);
    }

    [Fact]
    public void ProcessFrame_EnergyCalculation_IsCorrect()
    {
        // Arrange
        var vad = new VoiceActivityDetector();
        
        // Create frame with known amplitude
        var frame = CreateSineWaveFrame(1000, 0.3);
        
        // Act
        var result = vad.ProcessFrame(frame);
        
        // Assert - RMS of sine wave with amplitude A is A/sqrt(2) ≈ 0.707*A
        // So for amplitude 0.3, expect ~0.21
        Assert.InRange(result.Energy, 0.15, 0.25);
    }

    private static byte[] CreateSineWaveFrame(int frequencyHz, double amplitude)
    {
        const int sampleRate = 16000;
        const int samples = 960; // 60ms at 16kHz
        var frame = new byte[samples * 2]; // 16-bit samples
        
        for (int i = 0; i < samples; i++)
        {
            var t = i / (double)sampleRate;
            var value = amplitude * Math.Sin(2 * Math.PI * frequencyHz * t);
            var sample = (short)(value * 32767);
            
            frame[i * 2] = (byte)(sample & 0xFF);
            frame[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
        }
        
        return frame;
    }
}
