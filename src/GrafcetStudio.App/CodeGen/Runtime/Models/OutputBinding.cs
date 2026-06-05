namespace GrafcetStudio.CodeGen.Runtime.Models;

public class OutputBinding
{
    public string PhysicalOutputRef { get; init; } = string.Empty;

    public string BindingMode { get; init; } = "normal";

    public string SourceExecuteBitRef { get; init; } = string.Empty;

    public string ActionSymbol { get; init; } = string.Empty;

    public string Qualifier { get; init; } = string.Empty;

    public string DeviceLabel { get; init; } = string.Empty;

    public string DeviceFormat { get; init; } = string.Empty;

    public string CommandId { get; init; } = string.Empty;

    public string ActionLabel { get; init; } = string.Empty;

    public string DriveSignal { get; init; } = string.Empty;

    public IList<FeedbackSignalResult> FeedbackSignals { get; init; } = new List<FeedbackSignalResult>();
}
