using GrafcetStudio.CodeGen.Models;

namespace GrafcetStudio.CodeGen.Runtime.Models;

public class StepRuntimePlan
{
    public string StepId { get; init; } = string.Empty;

    public int StepNumber { get; init; }

    public string StepLabel { get; init; } = string.Empty;

    public string UnitId { get; init; } = string.Empty;

    public string Mode { get; init; } = string.Empty;

    public IList<string> PrevDoneRefs { get; init; } = new List<string>();

    public string TransitionRef { get; init; } = string.Empty;

    public string ExecuteBitRef { get; init; } = string.Empty;

    public string DoneBitRef { get; init; } = string.Empty;

    public IList<ActionResolveResult> ResolverResults { get; init; } = new List<ActionResolveResult>();

    public IList<string> FeedbackRefs { get; init; } = new List<string>();

    public string FeedbackAggregation { get; init; } = "AND";

    public IList<OutputBinding> OutputBindings { get; init; } = new List<OutputBinding>();

    public PlanValidation Validation { get; init; } = new();

    public IList<Diagnostic> Diagnostics { get; init; } = new List<Diagnostic>();
}
