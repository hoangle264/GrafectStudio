using System.Collections.Generic;

namespace GrafcetStudio.CodeGen.Runtime.Models;

public class DeviceCommandOutput
{
    public string CommandId { get; init; } = string.Empty;

    public string ActionLabel { get; init; } = string.Empty;

    public string DriveSignal { get; init; } = string.Empty;

    public string InterlockSignal { get; init; } = string.Empty;

    public string InterlockAddress { get; init; } = string.Empty;

    public string InterlockLabel { get; init; } = string.Empty;

    public string InterlockRequiredState { get; init; } = string.Empty;

    public bool HasInterlock { get; init; }

    public string PhysicalOutputRef { get; init; } = string.Empty;

    public string AggregationMode { get; init; } = "OR";

    public IList<string> SourceSteps { get; init; } = new List<string>();

    public IList<string> SourceExecuteBitRefs { get; init; } = new List<string>();

    public IList<string> SourceDoneBitRefs { get; init; } = new List<string>();

    public IList<DeviceCommandFlowOutput> FlowCommands { get; init; } = new List<DeviceCommandFlowOutput>();

    public int FlowCommandCount { get; init; }

    public int OriginCommandCount { get; init; }

    public int AutoCommandCount { get; init; }

    public bool HasOriginCommands { get; init; }

    public bool HasAutoCommands { get; init; }

    public IList<FeedbackSignalResult> FeedbackSignals { get; init; } = new List<FeedbackSignalResult>();
}

public class DeviceCommandFlowOutput
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string FlowType { get; init; } = string.Empty;

    public bool IsOrigin { get; init; }

    public bool IsAuto { get; init; }

    public string CommandId { get; init; } = string.Empty;

    public string ActionLabel { get; init; } = string.Empty;

    public string SourceStep { get; init; } = string.Empty;

    public string SourceExecuteBit { get; init; } = string.Empty;

    public string SourceDoneBit { get; init; } = string.Empty;

    public int Index { get; init; }

    public int Number { get; init; }

    public int TotalCount { get; init; }

    public bool IsFirst { get; init; }

    public bool IsLast { get; init; }

    public bool IsSingle { get; init; }
}

public class DeviceOutputGroup
{
    public string DeviceLabel { get; init; } = string.Empty;

    public string DeviceFormat { get; init; } = string.Empty;

    public string DeviceKind { get; init; } = "generic";

    public string? Address { get; init; }

    public IDictionary<string, string> SignalAddresses { get; init; } = new Dictionary<string, string>();

    public IDictionary<string, string> UnitAddresses { get; init; } = new Dictionary<string, string>();

    public IList<object> Signals { get; init; } = new List<object>();

    public IList<DeviceCommandOutput> Commands { get; init; } = new List<DeviceCommandOutput>();
}
