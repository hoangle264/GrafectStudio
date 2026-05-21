namespace GrafcetStudio.CodeGen.Runtime.Models;

public class AggregatedOutputBinding
{
    public string PhysicalOutputRef { get; init; } = string.Empty;

    public IList<string> SourceExecuteBitRefs { get; init; } = new List<string>();

    public IList<string> SourceSteps { get; init; } = new List<string>();

    public string AggregationMode { get; init; } = "OR";
}
