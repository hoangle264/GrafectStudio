using System.Collections.Generic;
using System.Text.Json.Serialization;
using GrafcetStudio.Domain.Enums;
using GrafcetStudio.Domain.Resolution;
using GrafcetStudio.CodeGen.Runtime.Models;

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

    [JsonIgnore]
    public bool DeviceCommandResolutionAttempted { get; init; }

    [JsonIgnore]
    public ActionResolveResult? ResolvedCommand { get; init; }

    [JsonIgnore]
    public string TargetAddress => ResolvedCommand?.OutputBindings.FirstOrDefault()?.PhysicalOutputRef 
        ?? (!string.IsNullOrWhiteSpace(Address) ? Address! : Variable);

    /// <summary>Lowercase alias for <see cref="TargetAddress"/>. Used by Handlebars templates via <c>{{target}}</c>.</summary>
    [JsonIgnore]
    public string target => TargetAddress;

    [JsonIgnore]
    public string? InterlockAddress => ResolvedCommand?.OutputBindings.FirstOrDefault()?.InterlockAddress;

    /// <summary>Lowercase alias for <see cref="InterlockAddress"/>. Used by Handlebars templates via <c>{{interlockAddress}}</c>.</summary>
    [JsonIgnore]
    public string? interlockAddress => InterlockAddress;

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
