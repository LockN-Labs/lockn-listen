using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime;
using Microsoft.Extensions.Options;
using LockNListen.Domain.Services;
using LockNListen.Domain.Models;
using LockNListen.Infrastructure.Sound;

namespace LockNListen.Infrastructure.Sound
{
    public class OnnxSoundClassifier : ISoundClassifier
    {
        private static string _modelPath;
        private static readonly Lazy<InferenceSession> _session = new(() => CreateSession());

        // YAMNet class indices mapped to target categories
        // Multiple indices map to same category (e.g., "Speech, male" and "Speech, female" → "Speech")
        private static readonly Dictionary<int, string> CategoryMap = new() {
            { 0, "Speech" }, { 1, "Speech" },   // Speech, male/female
            { 137, "Music" }, { 138, "Music" }, // Music, genre variants
            { 494, "Doorbell" },
            { 68, "Dog" },
            { 400, "Alarm" },
            { 401, "Alarm" },  // Fire alarm, smoke detector
        };

        private static InferenceSession CreateSession() {
            var options = new SessionOptions();
            try {
                options.AppendExecutionProvider_CUDA();
            } catch (Exception ex) {
                // CPU fallback - CUDA not available or failed to initialize
                // In production, consider logging: ex.Message
                System.Diagnostics.Debug.WriteLine($"CUDA unavailable, using CPU: {ex.Message}");
            }
            return new InferenceSession(_modelPath, options);
        }

        private static async Task EnsureModelExists(string modelPath) {
            if (!File.Exists(modelPath)) {
                Directory.CreateDirectory(Path.GetDirectoryName(modelPath)!);
                using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
                var bytes = await http.GetByteArrayAsync("https://github.com/onnx/models/raw/main/validated/vision/classification/yamnet/model/yamnet.onnx");
                await File.WriteAllBytesAsync(modelPath, bytes);
            }
        }

        private readonly SoundClassifierOptions _options;
        private readonly AudioPreprocessor _preprocessor = new();

        public OnnxSoundClassifier(IOptions<SoundClassifierOptions> options)
        {
            _options = options.Value;
            _modelPath = _options.ModelPath;
            EnsureModelExists(_modelPath).GetAwaiter().GetResult();
        }

        public async Task<SoundClassification> ClassifyAsync(byte[] audioData, int sampleRate)
        {
            // Preprocess audio data
            var processedData = _preprocessor.Resample(audioData, sampleRate, 16000);
            processedData = _preprocessor.ApplyWindow(processedData);

            // Create input tensor
            var inputTensor = new DenseTensor<float>(processedData.Select(b => (float)b).ToArray(), new[] { 1, processedData.Length });
            var inputs = new[] { Tuple.Create("input", inputTensor) };

            // Run inference
            using var results = _session.Run(inputs);
            var output = results.First().Value as DenseTensor<float>;

            // Map results
            return new SoundClassification
            {
                Category = GetPredictedCategory(output),
                Confidence = GetConfidenceScore(output),
                Timestamp = DateTime.UtcNow
            };
        }

        public async Task<SoundClassification> ClassifyAsync(Stream audioStream, CancellationToken ct = default)
        {
            using var ms = new MemoryStream();
            await audioStream.CopyToAsync(ms, ct);
            return await ClassifyAsync(ms.ToArray(), 16000);
        }

        private string GetPredictedCategory(DenseTensor<float> output) {
            var maxIndex = 0;
            var maxValue = float.MinValue;
            for (int i = 0; i < output.Length; i++) {
                if (output[i] > maxValue) {
                    maxValue = output[i];
                    maxIndex = i;
                }
            }
            return CategoryMap.TryGetValue(maxIndex, out var category) ? category : "Unknown";
        }

        private float GetConfidenceScore(DenseTensor<float> output) {
            return output.Max();
        }
    }
}
