using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using LockNListen.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LockNListen.Infrastructure.Speech;

/// <summary>
/// STT service that calls the faster-whisper HTTP API.
/// </summary>
public class FasterWhisperSttService : IStreamingSttService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FasterWhisperSttService> _logger;
    private readonly FasterWhisperOptions _options;
    private readonly JsonSerializerOptions _jsonOptions;

    public FasterWhisperSttService(
        HttpClient httpClient,
        ILogger<FasterWhisperSttService> logger,
        IOptions<FasterWhisperOptions> options)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true
        };
        
        _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
    }

    public async Task<StreamingTranscriptionResult> TranscribeSegmentAsync(
        byte[] audioData,
        string? language = null,
        CancellationToken cancellationToken = default)
    {
        var segmentId = Guid.NewGuid().ToString("N")[..8];
        _logger.LogDebug(
            "Transcribing segment {SegmentId}: {Bytes} bytes",
            segmentId, audioData.Length);

        try
        {
            // Create WAV file from raw PCM
            var wavData = CreateWavFromPcm(audioData, 16000, 16, 1);
            
            using var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(wavData);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
            content.Add(fileContent, "file", "audio.wav");
            
            if (!string.IsNullOrEmpty(language))
            {
                content.Add(new StringContent(language), "language");
            }

            var response = await _httpClient.PostAsync(
                "/v1/audio/transcriptions",
                content,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<WhisperApiResponse>(json, _jsonOptions);

            if (result == null)
            {
                throw new InvalidOperationException("Failed to parse transcription response");
            }

            var durationSeconds = audioData.Length / (16000.0 * 2); // 16kHz, 16-bit

            _logger.LogDebug(
                "Segment {SegmentId} transcribed: \"{Text}\" ({Duration:F2}s)",
                segmentId, result.Text?.Trim() ?? "", durationSeconds);

            return new StreamingTranscriptionResult
            {
                Text = result.Text?.Trim() ?? "",
                Language = result.Language,
                Confidence = result.Segments?.Count > 0 
                    ? result.Segments.Average(s => Math.Exp(s.Confidence)) 
                    : 0.9,
                DurationSeconds = durationSeconds,
                IsFinal = true,
                SegmentId = segmentId
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to call faster-whisper API");
            throw;
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogError(ex, "Faster-whisper API timed out");
            throw;
        }
    }

    /// <summary>
    /// Creates a WAV file from raw PCM data.
    /// </summary>
    private static byte[] CreateWavFromPcm(byte[] pcmData, int sampleRate, int bitsPerSample, int channels)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        var blockAlign = channels * bitsPerSample / 8;
        var byteRate = sampleRate * blockAlign;

        // RIFF header
        writer.Write("RIFF"u8);
        writer.Write(36 + pcmData.Length); // File size - 8
        writer.Write("WAVE"u8);

        // fmt chunk
        writer.Write("fmt "u8);
        writer.Write(16); // Subchunk1Size (16 for PCM)
        writer.Write((short)1); // AudioFormat (1 = PCM)
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write((short)blockAlign);
        writer.Write((short)bitsPerSample);

        // data chunk
        writer.Write("data"u8);
        writer.Write(pcmData.Length);
        writer.Write(pcmData);

        return ms.ToArray();
    }
}

/// <summary>
/// Configuration for faster-whisper API.
/// </summary>
public class FasterWhisperOptions
{
    /// <summary>Base URL of the faster-whisper API</summary>
    public string BaseUrl { get; set; } = "http://localhost:8890";

    /// <summary>Request timeout in seconds</summary>
    public int TimeoutSeconds { get; set; } = 30;
}

/// <summary>
/// Response from faster-whisper API.
/// </summary>
internal class WhisperApiResponse
{
    public string? Text { get; set; }
    public string? Language { get; set; }
    public double Duration { get; set; }
    public List<WhisperSegment>? Segments { get; set; }
}

internal class WhisperSegment
{
    public int Id { get; set; }
    public double Start { get; set; }
    public double End { get; set; }
    public string? Text { get; set; }
    
    [JsonPropertyName("confidence")]
    public double Confidence { get; set; } // avg_logprob
}
