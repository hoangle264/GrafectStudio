using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GrafcetStudio.Domain.Models;

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
    public List<DeviceVariable> Variables { get; set; } = new();

    public DiagramState ToDiagramState()
    {
        var connections = new List<Connection>();
        foreach (var transition in Transitions)
        {
            foreach (var fromStepId in transition.FromStepIds)
            {
                connections.Add(new Connection { From = fromStepId, To = transition.Id });
            }

            foreach (var toStepId in transition.ToStepIds)
            {
                connections.Add(new Connection { From = transition.Id, To = toStepId });
            }
        }

        return new DiagramState
        {
            Steps = Steps,
            Transitions = Transitions,
            Connections = connections,
            Variables = Variables
        };
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
}
