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

    public IList<DeviceCommandFlowOutput> OriginFlows { get; init; } = new List<DeviceCommandFlowOutput>();

    public IList<DeviceCommandFlowOutput> AutoFlows { get; init; } = new List<DeviceCommandFlowOutput>();

    public IList<FeedbackSignalResult> FeedbackSignals { get; init; } = new List<FeedbackSignalResult>();
}

public class DeviceCommandFlowOutput
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public IList<DeviceCommandFlowBitOutput> Commands { get; init; } = new List<DeviceCommandFlowBitOutput>();
}

public class DeviceCommandFlowBitOutput
{
    public string CommandId { get; init; } = string.Empty;

    public string ActionLabel { get; init; } = string.Empty;

    public string SourceStep { get; init; } = string.Empty;

    public string SourceExecuteBit { get; init; } = string.Empty;

    public string SourceDoneBit { get; init; } = string.Empty;
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