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

    [JsonPropertyName("ioMapping")]
    public IOMapping IOMapping { get; set; } = new();

    [JsonPropertyName("unitConfig")]
    public Dictionary<string, UnitConfig> UnitConfig { get; set; } = new();

    [JsonPropertyName("system")]
    public SystemControlInfo? System { get; set; }

    public void EnrichVariables()
    {
        // Signal IDs are stable keys from the web model; keep them unchanged for resolution.
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


