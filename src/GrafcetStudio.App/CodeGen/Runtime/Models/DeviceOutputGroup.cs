using System.Collections.Generic;

namespace GrafcetStudio.CodeGen.Runtime.Models;

public class DeviceCommandOutput
{
    public string CommandId { get; init; } = string.Empty;

    public string ActionLabel { get; init; } = string.Empty;

    public string DriveSignal { get; init; } = string.Empty;

    public string PhysicalOutputRef { get; init; } = string.Empty;

    public string AggregationMode { get; init; } = "OR";

    public IList<string> SourceSteps { get; init; } = new List<string>();

    public IList<string> SourceExecuteBitRefs { get; init; } = new List<string>();

    public IList<string> SourceDoneBitRefs { get; init; } = new List<string>();

    public IList<FeedbackSignalResult> FeedbackSignals { get; init; } = new List<FeedbackSignalResult>();
}

public class DeviceOutputGroup
{
    public string DeviceLabel { get; init; } = string.Empty;

    public string DeviceFormat { get; init; } = string.Empty;

    public string DeviceKind { get; init; } = "generic";

    public string? Address { get; init; }

    public IList<object> Signals { get; init; } = new List<object>();

    public IList<DeviceCommandOutput> Commands { get; init; } = new List<DeviceCommandOutput>();
}