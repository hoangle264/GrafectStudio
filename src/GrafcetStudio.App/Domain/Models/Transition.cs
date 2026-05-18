using System.Collections.Generic;
using GrafcetStudio.Domain.Resolution;

namespace GrafcetStudio.Domain.Models;

/// <summary>Represents a transition condition between Grafcet steps.</summary>
public class Transition
{
    public string Id { get; init; } = string.Empty;

    public string Condition { get; init; } = string.Empty;

    public string ResolveAddress(IList<DeviceVariable> vars)
        => SignalResolver.ResolveAddress(Condition, vars) ?? Condition;
}
