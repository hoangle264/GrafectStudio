using System.Collections.Generic;
using System.Text.Json.Serialization;
using GrafcetStudio.Domain.Enums;
using GrafcetStudio.Domain.Resolution;

namespace GrafcetStudio.Domain.Models;

/// <summary>Represents an action bound to a step in a Grafcet diagram.</summary>
public class StepAction
{
    [JsonPropertyName("variable")]
    public string Variable { get; init; } = string.Empty;

    [JsonPropertyName("address")]
    public string? Address { get; init; }

    [JsonPropertyName("qualifier")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ActionQualifier Qualifier { get; init; }

    [JsonPropertyName("timeMs")]
    public double TimeMs { get; init; }

    [JsonPropertyName("complete")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public StepActionCompletion? Complete { get; init; }

    [JsonPropertyName("sensorRef")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SensorRef { get; init; }

    public string ToPhysicalAddress(IList<DeviceVariable> vars)
        => !string.IsNullOrWhiteSpace(Address)
            ? Address!
            : SignalResolver.ResolveAddress(Variable, vars) ?? Variable;
}

public class StepActionCompletion
{
    [JsonPropertyName("sensor")]
    public string Sensor { get; init; } = string.Empty;

    [JsonPropertyName("sensorLabel")]
    public string SensorLabel { get; init; } = string.Empty;

    [JsonPropertyName("address")]
    public string Address { get; init; } = string.Empty;
}
