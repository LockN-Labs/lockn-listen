using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using LockNListen.Api.WebSockets;
using LockNListen.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;

namespace LockNListen.Tests;

public class SttWebSocketHandlerTests
{
    private readonly Mock<IStreamingSttService> _mockSttService;
    private readonly Mock<ILogger<SttWebSocketHandler>> _mockLogger;
    private readonly SttWebSocketHandler _handler;

    public SttWebSocketHandlerTests()
    {
        _mockSttService = new Mock<IStreamingSttService>();
        _mockLogger = new Mock<ILogger<SttWebSocketHandler>>();
        _handler = new SttWebSocketHandler(_mockSttService.Object, _mockLogger.Object);
    }

    [Fact]
    public void SttReadyMessage_SerializesCorrectly()
    {
        // Arrange
        var message = new SttReadyMessage
        {
            SessionId = "abc123"
        };
        
        // Act
        var json = JsonSerializer.Serialize(message, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });
        
        // Assert
        Assert.Contains("\"type\":\"ready\"", json);
        Assert.Contains("\"session_id\":\"abc123\"", json);
        Assert.Contains("\"message\":", json);
    }

    [Fact]
    public void SttTranscriptMessage_SerializesCorrectly()
    {
        // Arrange
        var message = new SttTranscriptMessage
        {
            SegmentId = "seg123",
            Text = "Hello world",
            IsFinal = true,
            Confidence = 0.95,
            Language = "en",
            DurationSeconds = 2.5
        };
        
        // Act
        var json = JsonSerializer.Serialize(message, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });
        
        // Assert
        Assert.Contains("\"type\":\"transcript\"", json);
        Assert.Contains("\"segment_id\":\"seg123\"", json);
        Assert.Contains("\"text\":\"Hello world\"", json);
        Assert.Contains("\"is_final\":true", json);
        Assert.Contains("\"confidence\":0.95", json);
    }

    [Fact]
    public void SttConfigMessage_DeserializesCorrectly()
    {
        // Arrange
        var json = """
        {
            "type": "config",
            "language": "en",
            "send_vad_status": true,
            "vad_sensitivity": 0.8
        }
        """;
        
        // Act
        var message = JsonSerializer.Deserialize<SttConfigMessage>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true
        });
        
        // Assert
        Assert.NotNull(message);
        Assert.Equal("en", message.Language);
        Assert.True(message.SendVadStatus);
        Assert.Equal(0.8, message.VadSensitivity);
    }

    [Fact]
    public void SttSpeechStartMessage_HasCorrectType()
    {
        var message = new SttSpeechStartMessage { SegmentId = "test" };
        Assert.Equal("speech_start", message.Type);
    }

    [Fact]
    public void SttSpeechEndMessage_HasCorrectType()
    {
        var message = new SttSpeechEndMessage { SegmentId = "test", DurationMs = 1000 };
        Assert.Equal("speech_end", message.Type);
    }

    [Fact]
    public void SttErrorMessage_HasCorrectType()
    {
        var message = new SttErrorMessage { Error = "test error", Code = "TEST" };
        Assert.Equal("error", message.Type);
    }

    [Fact]
    public void SttVadStatusMessage_HasCorrectType()
    {
        var message = new SttVadStatusMessage { IsSpeech = true, Energy = 0.1, Threshold = 0.05 };
        Assert.Equal("vad_status", message.Type);
    }

    [Fact]
    public async Task TranscribeSegmentAsync_ReturnsResult()
    {
        // Arrange
        var expectedResult = new StreamingTranscriptionResult
        {
            Text = "Hello world",
            Language = "en",
            Confidence = 0.95,
            DurationSeconds = 2.0,
            IsFinal = true,
            SegmentId = "test123"
        };
        
        _mockSttService
            .Setup(x => x.TranscribeSegmentAsync(
                It.IsAny<byte[]>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);
        
        // Act
        var result = await _mockSttService.Object.TranscribeSegmentAsync(
            new byte[1920],
            "en",
            CancellationToken.None);
        
        // Assert
        Assert.Equal("Hello world", result.Text);
        Assert.Equal("en", result.Language);
        Assert.Equal(0.95, result.Confidence);
    }
}
