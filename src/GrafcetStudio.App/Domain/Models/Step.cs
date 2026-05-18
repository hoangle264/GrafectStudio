using System.Collections.Generic;

namespace GrafcetStudio.Domain.Models;

/// <summary>Represents a Grafcet step with metadata and assigned actions.</summary>
public class Step
{
    public string Id { get; init; } = string.Empty;

    public int Number { get; init; }

    public string Label { get; init; } = string.Empty;

    public bool IsInitial { get; init; }

    public IList<StepAction> Actions { get; init; } = new List<StepAction>();
}
