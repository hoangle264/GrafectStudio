using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GrafcetStudio.App.Models;

public class CodegenPayload
{
    [JsonPropertyName("Platform")]
    public string Platform { get; set; } = string.Empty;

    [JsonPropertyName("Project")]
    public ProjectInfo? Project { get; set; }

    [JsonPropertyName("Diagram")]
    public DiagramInfo? Diagram { get; set; }

    [JsonPropertyName("Steps")]
    public List<Step> Steps { get; set; } = new();

    [JsonPropertyName("Transitions")]
    public List<Transition> Transitions { get; set; } = new();

    [JsonPropertyName("Variables")]
    public List<Variable> Variables { get; set; } = new();
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
}

public class Step
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("number")] public int Number { get; set; }
    [JsonPropertyName("label")] public string? Label { get; set; }
    [JsonPropertyName("initial")] public bool Initial { get; set; }
    [JsonPropertyName("actions")] public List<StepAction> Actions { get; set; } = new();
}

public class StepAction
{
    [JsonPropertyName("variable")] public string Variable { get; set; } = string.Empty;
    [JsonPropertyName("address")] public string? Address { get; set; }
    [JsonPropertyName("qualifier")] public string Qualifier { get; set; } = "N";
    [JsonPropertyName("time")] public string? Time { get; set; }
}

public class Transition
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("label")] public string? Label { get; set; }
    [JsonPropertyName("condition")] public string? Condition { get; set; }
    [JsonPropertyName("fromStepIds")] public List<string> FromStepIds { get; set; } = new();
    [JsonPropertyName("toStepIds")] public List<string> ToStepIds { get; set; } = new();
}

public class Variable
{
    [JsonPropertyName("label")] public string Label { get; set; } = string.Empty;
    [JsonPropertyName("address")] public string? Address { get; set; }
    [JsonPropertyName("format")] public string? Format { get; set; }
    [JsonPropertyName("signalAddresses")]
    public Dictionary<string, string>? SignalAddresses { get; set; }
    [JsonPropertyName("comment")] public string? Comment { get; set; }
}
