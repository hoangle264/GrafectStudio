using System.Collections.Generic;

namespace GrafcetStudio.Domain.Models;

/// <summary>Represents the full diagram state used for generation and analysis.</summary>
public class DiagramState
{
    public IList<Step> Steps { get; init; } = new List<Step>();

    public IList<Transition> Transitions { get; init; } = new List<Transition>();

    public IList<Connection> Connections { get; init; } = new List<Connection>();

    public IList<DeviceVariable> Variables { get; init; } = new List<DeviceVariable>();
}
