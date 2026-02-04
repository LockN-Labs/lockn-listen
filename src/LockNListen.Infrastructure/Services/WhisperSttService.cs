using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LockNListen.Domain.Interfaces;
using LockNListen.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Whisper.net;
using Whisper.net.Ggml;

namespace LockNListen.Infrastructure.Services;

/// <summary>
/// Speech-to-Text service using Whisper.net (local GPU-accelerated inference).
/// </summary>
public class WhisperSttService : ISttService, IAsyncDisposable
{
    private readonly ILogger<WhisperSttService> _logger;
    private readonly WhisperOptions _options;
    private readonly SemaphoreSlim _processorLock = new(1, 1);
    private WhisperProcessor? _processor;
    private bool _disposed;

    public WhisperSttService(
        ILogger<WhisperSttService> logger,
        IOptions<WhisperOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public async Task<TranscriptionResult> TranscribeAsync(
        byte[] audioData,
        string? language = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Transcribing {Bytes} bytes of audio with Whisper", audioData.Length);

        await EnsureProcessorInitializedAsync(cancellationToken);
        
        await _processorLock.WaitAsync(cancellationToken);
        try
        {
            var segments = new List<TranscriptionSegment>();
            var fullText = new System.Text.StringBuilder();
            var startTime = DateTime.UtcNow;

            // Process audio through Whisper
            await foreach (var segment in _processor!.ProcessAsync(audioData, cancellationToken))
            {
                segments.Add(new TranscriptionSegment
                {
                    Text = segment.Text,
                    Start = segment.Start,
                    End = segment.End,
                    Confidence = segment.Probability
                });
                fullText.Append(segment.Text);
            }

            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation(
                "Transcription completed in {Duration}ms, {Segments} segments",
                duration.TotalMilliseconds,
                segments.Count);

            return new TranscriptionResult
            {
                Text = fullText.ToString().Trim(),
                Confidence = segments.Count > 0 
                    ? segments.Average(s => s.Confidence) 
                    : 0.0,
                Duration = segments.Count > 0 
                    ? segments.Max(s => s.End) 
                    : TimeSpan.Zero,
                DetectedLanguage = language ?? _options.Language ?? "en",
                Segments = segments
            };
        }
        finally
        {
            _processorLock.Release();
        }
    }

    public async Task<TranscriptionResult> TranscribeAsync(
        Stream audioStream,
        string? language = null,
        CancellationToken cancellationToken = default)
    {
        using var ms = new MemoryStream();
        await audioStream.CopyToAsync(ms, cancellationToken);
        return await TranscribeAsync(ms.ToArray(), language, cancellationToken);
    }

    private async Task EnsureProcessorInitializedAsync(CancellationToken cancellationToken)
    {
        if (_processor != null) return;

        await _processorLock.WaitAsync(cancellationToken);
        try
        {
            if (_processor != null) return;

            _logger.LogInformation(
                "Initializing Whisper processor with model {Model}, GPU: {UseGpu}",
                _options.ModelSize,
                _options.UseGpu);

            var modelPath = await EnsureModelDownloadedAsync(_options.ModelSize, cancellationToken);
            
            var builder = WhisperFactory.FromPath(modelPath)
                .CreateBuilder()
                .WithLanguage(_options.Language ?? "en");

            if (_options.UseGpu)
            {
                builder.WithGpu();
            }

            _processor = builder.Build();
            
            _logger.LogInformation("Whisper processor initialized successfully");
        }
        finally
        {
            _processorLock.Release();
        }
    }

    private async Task<string> EnsureModelDownloadedAsync(string modelSize, CancellationToken cancellationToken)
    {
        var modelDir = _options.ModelPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LockNListen",
            "models",
            "whisper");
        
        Directory.CreateDirectory(modelDir);

        var ggmlType = modelSize.ToLowerInvariant() switch
        {
            "tiny" => GgmlType.Tiny,
            "base" => GgmlType.Base,
            "small" => GgmlType.Small,
            "medium" => GgmlType.Medium,
            "large" => GgmlType.LargeV3,
            _ => GgmlType.Small
        };

        var modelFileName = $"ggml-{modelSize}.bin";
        var modelPath = Path.Combine(modelDir, modelFileName);

        if (!File.Exists(modelPath))
        {
            _logger.LogInformation("Downloading Whisper model {Model} to {Path}", modelSize, modelPath);
            
            using var modelStream = await WhisperGgmlDownloader.GetGgmlModelAsync(ggmlType, cancellationToken);
            using var fileStream = File.Create(modelPath);
            await modelStream.CopyToAsync(fileStream, cancellationToken);
            
            _logger.LogInformation("Model download complete: {Path}", modelPath);
        }

        return modelPath;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _processor?.Dispose();
        _processorLock.Dispose();
        
        await Task.CompletedTask;
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Configuration options for Whisper STT service.
/// </summary>
public class WhisperOptions
{
    /// <summary>
    /// Path to store/load Whisper model files. Defaults to LocalApplicationData.
    /// </summary>
    public string? ModelPath { get; set; }

    /// <summary>
    /// Whisper model size: tiny, base, small, medium, large.
    /// </summary>
    public string ModelSize { get; set; } = "small";

    /// <summary>
    /// Default language code (ISO 639-1). Use null for auto-detection.
    /// </summary>
    public string? Language { get; set; } = "en";

    /// <summary>
    /// Enable GPU acceleration via CUDA.
    /// </summary>
    public bool UseGpu { get; set; } = true;
}
