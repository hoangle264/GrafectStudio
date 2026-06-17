namespace GrafcetStudio.App.Services.Ai;

public sealed record AiCompletionRequest(SanitizedAiRequest Request, string? FixtureName = null);

public sealed record AiCompletionResult(bool Ok, string RawText, IReadOnlyList<string> Errors)
{
    public static AiCompletionResult Success(string rawText) => new(true, rawText, Array.Empty<string>());

    public static AiCompletionResult Failure(params string[] errors) => new(false, string.Empty, errors);
}

public interface IAiCompletionService
{
    Task<AiCompletionResult> CompleteAsync(AiCompletionRequest request, CancellationToken cancellationToken = default);
}
