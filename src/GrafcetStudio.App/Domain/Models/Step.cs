using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GrafcetStudio.Domain.Models;

/// <summary>Represents a Grafcet step with metadata and assigned actions.</summary>
public class Step
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("number")]
    public int Number { get; init; }

    [JsonPropertyName("label")]
    public string Label { get; init; } = string.Empty;

    [JsonPropertyName("initial")]
    public bool IsInitial { get; init; }

    [JsonPropertyName("execAddress")]
    public string? ExecAddress { get; init; }

    [JsonPropertyName("doneAddress")]
    public string? DoneAddress { get; init; }

    [JsonPropertyName("actions")]
    public IList<StepAction> Actions { get; init; } = new List<StepAction>();
}
