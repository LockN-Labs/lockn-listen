using System;
using System.Buffers;

namespace LockNListen.Infrastructure.Speech;

/// <summary>
/// Energy-based Voice Activity Detector for real-time audio streaming.
/// Detects speech segments by monitoring audio energy levels.
/// </summary>
public class VoiceActivityDetector
{
    private readonly VadOptions _options;
    private readonly double[] _energyHistory;
    private int _energyHistoryIndex;
    private bool _isSpeaking;
    private int _silenceFrames;
    private int _speechFrames;
    private double _noiseFloor = 0.0;
    private int _noiseFloorSamples;
    private const int NoiseFloorCalibrationFrames = 50; // ~3 seconds at 60ms frames

    public VoiceActivityDetector() : this(new VadOptions()) { }
    
    public VoiceActivityDetector(VadOptions options)
    {
        _options = options;
        _energyHistory = new double[options.EnergyHistorySize];
        Reset();
    }

    /// <summary>
    /// Whether speech is currently detected.
    /// </summary>
    public bool IsSpeaking => _isSpeaking;

    /// <summary>
    /// Reset VAD state.
    /// </summary>
    public void Reset()
    {
        _isSpeaking = false;
        _silenceFrames = 0;
        _speechFrames = 0;
        _energyHistoryIndex = 0;
        _noiseFloor = 0.0;
        _noiseFloorSamples = 0;
        Array.Clear(_energyHistory, 0, _energyHistory.Length);
    }

    /// <summary>
    /// Process an audio frame and return VAD result.
    /// </summary>
    /// <param name="pcmData">16-bit PCM audio data</param>
    /// <returns>VAD decision for this frame</returns>
    public VadResult ProcessFrame(ReadOnlySpan<byte> pcmData)
    {
        var energy = CalculateEnergy(pcmData);
        
        // Update noise floor during calibration period
        if (_noiseFloorSamples < NoiseFloorCalibrationFrames)
        {
            _noiseFloor = ((_noiseFloor * _noiseFloorSamples) + energy) / (_noiseFloorSamples + 1);
            _noiseFloorSamples++;
        }

        // Store energy in history for smoothing
        _energyHistory[_energyHistoryIndex] = energy;
        _energyHistoryIndex = (_energyHistoryIndex + 1) % _energyHistory.Length;

        var smoothedEnergy = CalculateSmoothedEnergy();
        var threshold = Math.Max(_options.MinEnergyThreshold, _noiseFloor * _options.NoiseFloorMultiplier);
        var isSpeechFrame = smoothedEnergy > threshold;

        var previouslySpeaking = _isSpeaking;

        if (isSpeechFrame)
        {
            _speechFrames++;
            _silenceFrames = 0;

            // Start speaking if we have enough consecutive speech frames
            if (!_isSpeaking && _speechFrames >= _options.SpeechStartFrames)
            {
                _isSpeaking = true;
            }
        }
        else
        {
            _silenceFrames++;
            _speechFrames = 0;

            // Stop speaking if we have enough consecutive silence frames
            if (_isSpeaking && _silenceFrames >= _options.SilenceEndFrames)
            {
                _isSpeaking = false;
            }
        }

        return new VadResult
        {
            IsSpeech = _isSpeaking,
            Energy = energy,
            SmoothedEnergy = smoothedEnergy,
            Threshold = threshold,
            JustStartedSpeaking = _isSpeaking && !previouslySpeaking,
            JustStoppedSpeaking = !_isSpeaking && previouslySpeaking
        };
    }

    private double CalculateEnergy(ReadOnlySpan<byte> pcmData)
    {
        if (pcmData.Length < 2) return 0;

        var sampleCount = pcmData.Length / 2;
        double sumSquares = 0;

        for (int i = 0; i < pcmData.Length - 1; i += 2)
        {
            var sample = (short)(pcmData[i] | (pcmData[i + 1] << 8));
            var normalized = sample / 32768.0;
            sumSquares += normalized * normalized;
        }

        return Math.Sqrt(sumSquares / sampleCount); // RMS energy
    }

    private double CalculateSmoothedEnergy()
    {
        double sum = 0;
        for (int i = 0; i < _energyHistory.Length; i++)
        {
            sum += _energyHistory[i];
        }
        return sum / _energyHistory.Length;
    }
}

/// <summary>
/// VAD configuration options.
/// </summary>
public class VadOptions
{
    /// <summary>Minimum energy threshold (prevents false positives in silence)</summary>
    public double MinEnergyThreshold { get; set; } = 0.01;

    /// <summary>Multiplier over noise floor to detect speech</summary>
    public double NoiseFloorMultiplier { get; set; } = 2.5;

    /// <summary>Number of consecutive speech frames to start utterance (60ms each)</summary>
    public int SpeechStartFrames { get; set; } = 3; // ~180ms

    /// <summary>Number of consecutive silence frames to end utterance (60ms each)</summary>
    public int SilenceEndFrames { get; set; } = 15; // ~900ms

    /// <summary>Size of energy history buffer for smoothing</summary>
    public int EnergyHistorySize { get; set; } = 5;
}

/// <summary>
/// Result of VAD processing for a single frame.
/// </summary>
public record VadResult
{
    /// <summary>Whether speech is currently active</summary>
    public bool IsSpeech { get; init; }
    
    /// <summary>Raw frame energy</summary>
    public double Energy { get; init; }
    
    /// <summary>Smoothed energy level</summary>
    public double SmoothedEnergy { get; init; }
    
    /// <summary>Current energy threshold</summary>
    public double Threshold { get; init; }
    
    /// <summary>Speech just started this frame</summary>
    public bool JustStartedSpeaking { get; init; }
    
    /// <summary>Speech just ended this frame</summary>
    public bool JustStoppedSpeaking { get; init; }
}
