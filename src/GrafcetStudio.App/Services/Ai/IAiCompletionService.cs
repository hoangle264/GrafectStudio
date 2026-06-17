namespace GrafcetStudio.App.Services.Ai;

public sealed record AiCompletionRequest(SanitizedAiRequest Request, string? FixtureName = null);

public sealed record AiCompletionResult(bool Ok, string RawText, IReadOnlyList<string> Errors)
{
    public static AiCompletionResult Success(string rawText) => new(true, rawText, Array.Empty<string>());

    public static AiCompletionResult Failure(params string[] errors) => new(false, string.Empty, errors);
}

public sealed record AiStreamChunk(string Kind, string Text, bool IsFinal = false)
{
    public static AiStreamChunk Status(string text) => new("status", text);

    public static AiStreamChunk Delta(string text) => new("delta", text);

    public static AiStreamChunk Final(string text) => new("final", text, true);
}

public interface IAiCompletionService
{
    Task<AiCompletionResult> CompleteAsync(AiCompletionRequest request, CancellationToken cancellationToken = default);

    async IAsyncEnumerable<AiStreamChunk> StreamAsync(AiCompletionRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return AiStreamChunk.Status("Generating proposal...");
        var result = await CompleteAsync(request, cancellationToken);
        if (!result.Ok)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors));
        }

        yield return AiStreamChunk.Final(result.RawText);
    }
}
