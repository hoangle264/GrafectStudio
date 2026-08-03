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

    /// <summary>Expression-first shape for new Keyence templates: condition -> instruction target.</summary>
    public DeviceOutputIntent output { get; init; } = new();

    public IList<DeviceCommandFlowOutput> sources => output.sources;

    public string instruction => output.instruction;

    public string target => output.target;

    public string mnemonic => output.mnemonic;

    public IList<string> mnemonicLines => output.mnemonicLines;
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

    public string actionSymbol { get; init; } = string.Empty;

    public string qualifier { get; init; } = string.Empty;

    public string modeFlagAddress { get; init; } = string.Empty;

    public string conditionMnemonic { get; init; } = string.Empty;

    public IList<string> conditionMnemonicLines { get; init; } = new List<string>();

    public bool hasCondition => !string.IsNullOrWhiteSpace(conditionMnemonic);
}

public class DeviceOutputIntent
{
    public int index { get; init; }

    public int number { get; init; }

    public string deviceLabel { get; init; } = string.Empty;

    public string deviceFormat { get; init; } = string.Empty;

    public string deviceKind { get; init; } = "generic";

    public string commandId { get; init; } = string.Empty;

    public string actionLabel { get; init; } = string.Empty;

    public string driveSignal { get; init; } = string.Empty;

    public string conditionExpression => driveConditionExpression;

    public string sourceConditionExpression { get; init; } = string.Empty;

    public string autoConditionExpression { get; init; } = string.Empty;

    public string originConditionExpression { get; init; } = string.Empty;

    public string manualConditionExpression { get; init; } = string.Empty;

    public string interlockExpression { get; init; } = string.Empty;

    public string driveConditionExpression { get; init; } = string.Empty;

    public string instruction { get; init; } = string.Empty;

    public string target { get; init; } = string.Empty;

    public string expression { get; init; } = string.Empty;

    public string mnemonic { get; init; } = string.Empty;

    public IList<string> mnemonicLines { get; init; } = new List<string>();

    public IList<DeviceCommandFlowOutput> sources { get; init; } = new List<DeviceCommandFlowOutput>();

    public IList<FeedbackSignalResult> feedbackSignals { get; init; } = new List<FeedbackSignalResult>();

    public bool hasCondition => !string.IsNullOrWhiteSpace(conditionExpression);

    public bool hasAutoCondition => !string.IsNullOrWhiteSpace(autoConditionExpression);

    public bool hasOriginCondition => !string.IsNullOrWhiteSpace(originConditionExpression);

    public bool hasManualCondition => !string.IsNullOrWhiteSpace(manualConditionExpression);

    public bool hasInterlock => !string.IsNullOrWhiteSpace(interlockExpression);

    public bool hasFeedback => feedbackSignals.Count > 0;
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

    /// <summary>Flat expression-first outputs for templates that do not need legacy command binding details.</summary>
    public IList<DeviceOutputIntent> outputs { get; init; } = new List<DeviceOutputIntent>();
}
