using System.Collections.Generic;
using System.Text.Json.Serialization;
using GrafcetStudio.Domain.Resolution;

namespace GrafcetStudio.Domain.Models;

/// <summary>Represents a transition condition between Grafcet steps.</summary>
public class Transition
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; init; } = string.Empty;

    [JsonPropertyName("condition")]
    public string Condition { get; init; } = string.Empty;

    [JsonPropertyName("fromStepIds")]
    public IList<string> FromStepIds { get; init; } = new List<string>();

    [JsonPropertyName("toStepIds")]
    public IList<string> ToStepIds { get; init; } = new List<string>();

    public string ResolveAddress(IList<DeviceVariable> vars)
        => SignalResolver.ResolveAddress(Condition, vars) ?? Condition;
}
