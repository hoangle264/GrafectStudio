using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq;

namespace GrafcetStudio.Domain.Models;

public class CodegenPayload
{
    [JsonPropertyName("platform")]
    public string Platform { get; set; } = string.Empty;

    [JsonPropertyName("templateRootPath")]
    public string TemplateRootPath { get; set; } = string.Empty;

    [JsonPropertyName("project")]
    public ProjectInfo? Project { get; set; }

    [JsonPropertyName("unit")]
    public UnitInfo? Unit { get; set; }

    [JsonPropertyName("units")]
    public List<UnitInfo> Units { get; set; } = new();

    [JsonPropertyName("flows")]
    public List<FlowInfo> Flows { get; set; } = new();

    [JsonPropertyName("variables")]
    public List<DeviceVariable> Variables { get; set; } = new();

    [JsonPropertyName("blocks")]
    public List<PlcBlock> Blocks { get; set; } = new();

    [JsonPropertyName("deviceTypes")]
    public List<DeviceType> DeviceTypes { get; set; } = new();

    [JsonPropertyName("deviceLibraryPath")]
    public string DeviceLibraryPath { get; set; } = string.Empty;

    [JsonPropertyName("templateProfile")]
    public string TemplateProfile { get; set; } = "simple";

    /// <summary>SimaticML version for Siemens generators (19, 20, ...). 0 = use the template's TIA_VERSION.</summary>
    [JsonPropertyName("tiaVersion")]
    public uint TiaVersion { get; set; }

    [JsonPropertyName("ioMapping")]
    public IOMapping IOMapping { get; set; } = new();

    [JsonPropertyName("unitConfig")]
    public Dictionary<string, UnitConfig> UnitConfig { get; set; } = new();

    [JsonPropertyName("system")]
    public SystemControlInfo? System { get; set; }

    [JsonPropertyName("sharedFlowStruct")]
    public SharedFlowStructSchema? SharedFlowStruct { get; set; }

    public void EnrichVariables()
    {
        if (SharedFlowStruct != null && SharedFlowStruct.Enabled && SharedFlowStruct.Members.Count == 0)
        {
            var targetName = SharedFlowStruct.StructTypeName;
            if (string.IsNullOrWhiteSpace(targetName)) targetName = "UDT_FlowData";

            var matchingDevice = DeviceTypes.FirstOrDefault(d => string.Equals(d.Name, targetName, StringComparison.OrdinalIgnoreCase));
            if (matchingDevice != null && matchingDevice.Signals != null && matchingDevice.Signals.Count > 0)
            {
                foreach (var s in matchingDevice.Signals)
                {
                    SharedFlowStruct.Members.Add(new SharedFlowStructMember
                    {
                        Id = s.Id,
                        Name = s.Name,
                        Type = s.DataType ?? "BOOL",
                        Comment = s.Comment
                    });
                }
            }
        }

        ApplyLabelFallback();
    }

    /// <summary>
    /// Fills every missing PLC address with its label (<c>{Label}</c> for a plain variable,
    /// <c>{Label}.{Signal}</c> for a device/unit/system signal) so generators and templates
    /// reference the label instead of emitting an empty operand.
    /// </summary>
    public void ApplyLabelFallback()
    {
        var deviceTypesByName = DeviceTypes
            .Where(type => !string.IsNullOrWhiteSpace(type.Name))
            .GroupBy(type => type.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var variable in Variables)
        {
            deviceTypesByName.TryGetValue(variable.Format, out var deviceType);
            var signalNames = deviceType?.Signals
                .Where(signal => !variable.SignalAddresses.ContainsKey(signal.Id) && !variable.SignalAddresses.ContainsKey(signal.Name))
                .Select(signal => string.IsNullOrWhiteSpace(signal.Name) ? signal.Id : signal.Name);
            FillSignalLabels(variable.Label, variable.SignalAddresses, signalNames);

            if (variable.SignalAddresses.Count == 0 && string.IsNullOrWhiteSpace(variable.Address))
            {
                variable.Address = variable.Label;
            }
        }

        foreach (var (key, cfg) in UnitConfig)
        {
            FillSignalLabels(string.IsNullOrWhiteSpace(cfg.Label) ? key : cfg.Label, cfg.SignalAddresses, null);
        }

        if (System is not null) FillSignalLabels(System.Label, System.SignalAddresses, null);
    }

    private static void FillSignalLabels(string ownerLabel, IDictionary<string, string> signalAddresses, IEnumerable<string>? missingSignals)
    {
        if (string.IsNullOrWhiteSpace(ownerLabel)) return;

        foreach (var signal in missingSignals ?? Enumerable.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(signal)) signalAddresses[signal] = string.Empty;
        }

        foreach (var signal in signalAddresses.Keys.ToList())
        {
            if (string.IsNullOrWhiteSpace(signalAddresses[signal])) signalAddresses[signal] = $"{ownerLabel}.{signal}";
        }
    }
}

public class CodegenFile
{
    [JsonPropertyName("path")] public string Path { get; set; } = string.Empty;
    [JsonPropertyName("content")] public string Content { get; set; } = string.Empty;
}

public class CodegenOutput
{
    [JsonPropertyName("files")] public List<CodegenFile> Files { get; set; } = new();
}

public class ProjectInfo
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("machineName")] public string? MachineName { get; set; }
    [JsonPropertyName("plc")] public PlcPayloadInfo? Plc { get; set; }
}


public class PlcPayloadInfo
{
    [JsonPropertyName("namePlc")] public string? NamePlc { get; set; }
    [JsonPropertyName("deviceCode")] public string? DeviceCode { get; set; }
}
public class DiagramInfo
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("mode")] public string? Mode { get; set; }
    [JsonPropertyName("diagramType")] public string? DiagramType { get; set; } = "Macro";
    [JsonPropertyName("controlState")] public string? ControlState { get; set; }
    [JsonPropertyName("category")] public string? Category { get; set; }
    [JsonPropertyName("orchestratorConfig")] public OrchestratorConfig? OrchestratorConfig { get; set; }
    [JsonPropertyName("unitId")] public string? UnitId { get; set; }
    [JsonPropertyName("unit")] public string? Unit { get; set; }
    [JsonPropertyName("addressMode")] public string? AddressMode { get; set; }
    [JsonPropertyName("boolAddressMode")] public string? BoolAddressMode { get; set; }
    [JsonPropertyName("baseMr")] public string? BaseMr { get; set; }
    [JsonPropertyName("activeWord")] public string? ActiveWord { get; set; }
    [JsonPropertyName("activeWordTag")] public string? ActiveWordTag { get; set; }
    [JsonPropertyName("completeWord")] public string? CompleteWord { get; set; }
    [JsonPropertyName("completeWordTag")] public string? CompleteWordTag { get; set; }
}

public class OrchestratorConfig
{
    [JsonPropertyName("elements")] public List<OrchestratorElement> Elements { get; set; } = new();
}

public class OrchestratorElement
{
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("config")] public JsonElement? Config { get; set; }
}

public class UnitInfo
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("label")] public string? Label { get; set; }
}

public class FlowInfo
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("mode")] public string? Mode { get; set; }
    [JsonPropertyName("diagramType")] public string? DiagramType { get; set; } = "Macro";
    [JsonPropertyName("controlState")] public string? ControlState { get; set; }
    [JsonPropertyName("category")] public string? Category { get; set; }
    [JsonPropertyName("orchestratorConfig")] public OrchestratorConfig? OrchestratorConfig { get; set; }
    [JsonPropertyName("diagram")] public DiagramInfo? Diagram { get; set; }
    [JsonPropertyName("steps")] public List<Step> Steps { get; set; } = new();
    [JsonPropertyName("transitions")] public List<Transition> Transitions { get; set; } = new();
    [JsonPropertyName("macroPortVariable")] public DeviceVariable? MacroPortVariable { get; set; }
    [JsonPropertyName("structInstanceName")] public string? StructInstanceName { get; set; }
    [JsonPropertyName("structTypeName")] public string? StructTypeName { get; set; }
    [JsonPropertyName("flowVariable")] public DeviceVariable? FlowVariable { get; set; }
}

public class UnitConfig
{
    [JsonPropertyName("label")] public string Label { get; set; } = string.Empty;
    [JsonPropertyName("signalAddresses")] public Dictionary<string, string> SignalAddresses { get; set; } = new();
}

public class SystemControlInfo
{
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("signalAddresses")]
    public IDictionary<string, string> SignalAddresses { get; set; } = new Dictionary<string, string>();
}

public class IOMapping
{
    [JsonPropertyName("physicalIOs")] public List<PhysicalIO> PhysicalIOs { get; set; } = new();
    [JsonPropertyName("entries")] public List<IOMappingEntry> Entries { get; set; } = new();
}

public class PhysicalIO
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("deviceTag")] public string DeviceTag { get; set; } = string.Empty;
    [JsonPropertyName("plcAddress")] public string PlcAddress { get; set; } = string.Empty;
    [JsonPropertyName("direction")] public string Direction { get; set; } = string.Empty;
    [JsonPropertyName("description")] public string? Description { get; set; }
}

public class IOMappingEntry
{
    [JsonPropertyName("physicalIOId")] public string PhysicalIOId { get; set; } = string.Empty;
    [JsonPropertyName("appVariable")] public string AppVariable { get; set; } = string.Empty;
    [JsonPropertyName("status")] public string Status { get; set; } = string.Empty;
    [JsonPropertyName("matchScore")] public double MatchScore { get; set; }
}

public class SharedFlowStructMember
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("type")] public string Type { get; set; } = "BOOL";
    [JsonPropertyName("comment")] public string? Comment { get; set; }
    [JsonPropertyName("defaultValue")] public string? DefaultValue { get; set; }
}

public class SharedFlowStructSchema
{
    [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;
    [JsonPropertyName("structTypeName")] public string StructTypeName { get; set; } = "UDT_FlowData";
    [JsonPropertyName("members")] public List<SharedFlowStructMember> Members { get; set; } = new();
}


