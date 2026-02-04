using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using LockNListen.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace LockNListen.Infrastructure.Services
{
    public class LockNLoggerClient : IReceiptLogger
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<LockNLoggerClient> _logger;

        public LockNLoggerClient(HttpClient httpClient, ILogger<LockNLoggerClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task LogTranscriptionReceiptAsync(TimeSpan audioDuration, string model, TimeSpan latency, string language)
        {
            try
            {
                var receipt = new
                {
                    provider = "lockn-listen",
                    model = model,
                    durationSeconds = audioDuration.TotalSeconds,
                    latencyMs = latency.TotalMilliseconds,
                    language = language
                };

                var json = JsonSerializer.Serialize(receipt);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Fire-and-forget pattern - don't await the result
                _ = _httpClient.PostAsync("/api/receipts", content);
            }
            catch (Exception ex)
            {
                // Fire-and-forget pattern: log but don't throw
                _logger.LogWarning(ex, "Failed to log transcription receipt to LockN Logger");
            }
        }
    }
}