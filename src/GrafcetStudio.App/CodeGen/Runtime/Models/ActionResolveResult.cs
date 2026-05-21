using GrafcetStudio.CodeGen.Models;

namespace GrafcetStudio.CodeGen.Runtime.Models;

public class ExecuteSignalResult
{
    public string? SignalName { get; init; }

    public string PhysicalAddress { get; init; } = string.Empty;
}

public class FeedbackSignalResult
{
    public string SignalName { get; init; } = string.Empty;

    public string PhysicalAddress { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public bool Required { get; init; } = true;
}

public class ActionResolveResult
{
    public string Status { get; init; } = "ok";

    public string ActionSymbol { get; init; } = string.Empty;

    public string Qualifier { get; init; } = string.Empty;

    public ExecuteSignalResult Execute { get; init; } = new();

    public IList<FeedbackSignalResult> FeedbackSignals { get; init; } = new List<FeedbackSignalResult>();

    public IList<OutputBinding> OutputBindings { get; init; } = new List<OutputBinding>();

    public double TimeoutMs { get; init; }

    public string CompletionMode { get; init; } = "all-feedback-on";

    public IList<Diagnostic> Diagnostics { get; init; } = new List<Diagnostic>();
}
