namespace GrafcetStudio.CodeGen.Runtime.Models;

public class AggregatedOutputBinding
{
    public string PhysicalOutputRef { get; init; } = string.Empty;

    public IList<string> SourceExecuteBitRefs { get; init; } = new List<string>();

    public IList<string> SourceDoneBitRefs { get; init; } = new List<string>();

    public IList<string> SourceSteps { get; init; } = new List<string>();

    public string AggregationMode { get; init; } = "OR";

    public IList<OutputBindingSource> Sources { get; init; } = new List<OutputBindingSource>();
}

public class OutputBindingSource
{
    public string StepId { get; init; } = string.Empty;

    public int StepNumber { get; init; }

    public string StepLabel { get; init; } = string.Empty;

    public string SourceStep { get; init; } = string.Empty;

    public string SourceExecuteBitRef { get; init; } = string.Empty;

    public string SourceDoneBitRef { get; init; } = string.Empty;

    public string ActionSymbol { get; init; } = string.Empty;

    public string Qualifier { get; init; } = string.Empty;

    public string DeviceLabel { get; init; } = string.Empty;

    public string DeviceFormat { get; init; } = string.Empty;

    public string CommandId { get; init; } = string.Empty;

    public string ActionLabel { get; init; } = string.Empty;

    public string DriveSignal { get; init; } = string.Empty;

    public IList<FeedbackSignalResult> FeedbackSignals { get; init; } = new List<FeedbackSignalResult>();
}
