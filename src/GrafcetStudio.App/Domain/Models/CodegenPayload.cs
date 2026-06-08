using System.Collections.Generic;
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

    [JsonPropertyName("flows")]
    public List<FlowInfo> Flows { get; set; } = new();

    [JsonPropertyName("variables")]
    public List<DeviceVariable> Variables { get; set; } = new();

    [JsonPropertyName("deviceTypes")]
    public List<DeviceType> DeviceTypes { get; set; } = new();

    [JsonPropertyName("deviceLibraryPath")]
    public string DeviceLibraryPath { get; set; } = string.Empty;

    public void EnrichVariables()
    {
        // Signal IDs are stable keys from the web model; keep them unchanged for resolution.
    }
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
    [JsonPropertyName("unitId")] public string? UnitId { get; set; }
    [JsonPropertyName("unit")] public string? Unit { get; set; }
    [JsonPropertyName("addressMode")] public string? AddressMode { get; set; }
    [JsonPropertyName("boolAddressMode")] public string? BoolAddressMode { get; set; }
    [JsonPropertyName("baseMr")] public int? BaseMr { get; set; }
    [JsonPropertyName("activeWord")] public string? ActiveWord { get; set; }
    [JsonPropertyName("completeWord")] public string? CompleteWord { get; set; }
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
    [JsonPropertyName("diagram")] public DiagramInfo? Diagram { get; set; }
    [JsonPropertyName("steps")] public List<Step> Steps { get; set; } = new();
    [JsonPropertyName("transitions")] public List<Transition> Transitions { get; set; } = new();
}

