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

    [JsonPropertyName("deviceTypes")]
    public List<DeviceType> DeviceTypes { get; set; } = new();

    [JsonPropertyName("deviceLibraryPath")]
    public string DeviceLibraryPath { get; set; } = string.Empty;

    [JsonPropertyName("templateProfile")]
    public string TemplateProfile { get; set; } = "simple";

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
    [JsonPropertyName("baseMr")] public int? BaseMr { get; set; }
    [JsonPropertyName("activeWord")] public string? ActiveWord { get; set; }
    [JsonPropertyName("completeWord")] public string? CompleteWord { get; set; }
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
}

