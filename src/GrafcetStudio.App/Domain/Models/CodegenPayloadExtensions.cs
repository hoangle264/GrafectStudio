using System.Collections.Generic;
using System.Linq;

namespace GrafcetStudio.Domain.Models;

public static class CodegenPayloadExtensions
{
    public static DiagramState ToDiagramState(this CodegenPayload payload)
    {
        var variables = payload.Variables ?? new List<DeviceVariable>();
        var flow = payload.Flows.FirstOrDefault();
        return flow is null
            ? new DiagramState { Steps = new List<Step>(), Transitions = new List<Transition>(), Connections = new List<Connection>(), Variables = variables }
            : flow.ToDiagramState(variables);
    }
}

public static class FlowInfoExtensions
{
    public static DiagramState ToDiagramState(this FlowInfo flow, IList<DeviceVariable> variables)
    {
        var connections = flow.Transitions.SelectMany(t =>
            t.FromStepIds.Select(stepId => new Connection { From = stepId, To = t.Id })
                .Concat(t.ToStepIds.Select(stepId => new Connection { From = t.Id, To = stepId }))).ToList();

        return new DiagramState
        {
            Steps = flow.Steps ?? new List<Step>(),
            Transitions = flow.Transitions ?? new List<Transition>(),
            Connections = connections,
            Variables = variables
        };
    }
}
