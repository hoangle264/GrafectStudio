using GrafcetStudio.CodeGen.Models;

namespace GrafcetStudio.CodeGen.Runtime.Models;

public class OutputBindingPlan
{
    public IList<AggregatedOutputBinding> Bindings { get; init; } = new List<AggregatedOutputBinding>();

    public IList<Diagnostic> Diagnostics { get; init; } = new List<Diagnostic>();
}
