using System.Collections.Generic;
using GrafcetStudio.Domain.Enums;
using GrafcetStudio.Domain.Resolution;

namespace GrafcetStudio.Domain.Models;

/// <summary>Represents an action bound to a step in a Grafcet diagram.</summary>
public class StepAction
{
    public string Variable { get; init; } = string.Empty;

    public string? Address { get; init; }

    public ActionQualifier Qualifier { get; init; }

    public double TimeMs { get; init; }

    public string ToPhysicalAddress(IList<DeviceVariable> vars)
        => !string.IsNullOrWhiteSpace(Address)
            ? Address!
            : SignalResolver.ResolveAddress(Variable, vars) ?? Variable;
}
