## 🏗️ Phase 2: Architecture Complete
**Agent:** Codex (architect)

**Design:**
Following the lockn-voice pattern exactly, I'll implement a LockN Logger integration for STT usage tracking. The solution will:

1. Create an `IReceiptLogger` interface in the Domain layer for consistent logging contract
2. Create a `LockNLoggerClient` in Infrastructure that implements this interface, following fire-and-forget pattern
3. Modify TranscriptionEndpoints to inject and use the logger
4. Add configuration for LockNLogger base URL
5. Use IHttpClientFactory for HttpClient injection
6. Graceful degradation if logger is unavailable

**Subtasks dispatched to Qwen3-Coder:**
1. `src/LockNListen.Domain/Interfaces/IReceiptLogger.cs` — Create receipt logger interface
2. `src/LockNListen.Infrastructure/Services/LockNLoggerClient.cs` — Implement logger client
3. `src/LockNListen.Api/Endpoints/TranscriptionEndpoints.cs` — Inject and use logger in transcription
4. `src/LockNListen.Api/Program.cs` — Register logger with DI
5. `src/LockNListen.Api/appsettings.json` — Add LockNLogger configuration

**Interfaces defined:**
- `IReceiptLogger` with `LogTranscriptionReceiptAsync` method
- DTO for receipt data with audio duration, model, latency, language info

**Key Implementation Details:**
- Follow fire-and-forget pattern from lockn-voice
- Manual mapping (no AutoMapper)
- Use IHttpClientFactory for HttpClient injection
- Log: audio duration, model used, latency, language
- Graceful failure handling