using System.Net.Http;

namespace GrafcetStudio.App.Services.Ai;

/// <summary>
/// Picks Gemini or the mock service per call, so a key saved from the UI takes effect without restarting.
/// </summary>
public class DynamicAiCompletionService : IAiCompletionService
{
    private static readonly HttpClient SharedHttpClient = new();
    private readonly AiKeyStore _keyStore;
    private readonly MockAiCompletionService _mock = new();

    public DynamicAiCompletionService(AiKeyStore keyStore)
    {
        _keyStore = keyStore;
    }

    public Task<AiCompletionResult> CompleteAsync(AiCompletionRequest request, CancellationToken cancellationToken = default)
        => Resolve(request).CompleteAsync(request, cancellationToken);

    public IAsyncEnumerable<AiStreamChunk> StreamAsync(AiCompletionRequest request, CancellationToken cancellationToken = default)
        => Resolve(request).StreamAsync(request, cancellationToken);

    /// <summary>True when requests will reach a real AI provider (not the mock).</summary>
    public bool IsLive => !IsMockForced() && !string.IsNullOrWhiteSpace(_keyStore.ResolveKey().Key);

    private IAiCompletionService Resolve(AiCompletionRequest request)
    {
        if (IsMockForced()) return _mock;

        var (key, _) = _keyStore.ResolveKey();
        if (string.IsNullOrWhiteSpace(key)) return _mock;

        var model = _keyStore.ResolveModel();
        AiAuditLog.Record(request.Request, "gemini", model);
        return new GeminiAiCompletionService(SharedHttpClient, key, model);
    }

    private static bool IsMockForced()
        => string.Equals(Environment.GetEnvironmentVariable("GRAFCETSTUDIO_AI_MODE")?.Trim(), "mock", StringComparison.OrdinalIgnoreCase);
}
