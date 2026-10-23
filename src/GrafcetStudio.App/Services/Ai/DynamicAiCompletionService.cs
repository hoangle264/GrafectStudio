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
        => Resolve().CompleteAsync(request, cancellationToken);

    public IAsyncEnumerable<AiStreamChunk> StreamAsync(AiCompletionRequest request, CancellationToken cancellationToken = default)
        => Resolve().StreamAsync(request, cancellationToken);

    private IAiCompletionService Resolve()
    {
        if (IsMockForced()) return _mock;

        var (key, _) = _keyStore.ResolveKey();
        return string.IsNullOrWhiteSpace(key)
            ? _mock
            : new GeminiAiCompletionService(SharedHttpClient, key, _keyStore.ResolveModel());
    }

    private static bool IsMockForced()
        => string.Equals(Environment.GetEnvironmentVariable("GRAFCETSTUDIO_AI_MODE")?.Trim(), "mock", StringComparison.OrdinalIgnoreCase);
}
