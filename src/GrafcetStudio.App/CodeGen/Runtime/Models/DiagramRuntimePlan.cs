using GrafcetStudio.Domain.Models;

namespace GrafcetStudio.CodeGen.Runtime.Models;

public class DiagramRuntimePlan
{
    public DiagramMeta Diagram { get; init; } = new();

    public int BaseMr { get; init; }

    public IList<StepRuntimePlan> StepPlans { get; init; } = new List<StepRuntimePlan>();

    public OutputBindingPlan OutputBindingPlan { get; init; } = new();
}
