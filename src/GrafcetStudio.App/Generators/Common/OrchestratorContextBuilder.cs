using GrafcetStudio.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GrafcetStudio.App.Generators.Common;

/// <summary>
/// Validates orchestrator flows and resolves them into <see cref="OrchestratorContext"/> stages.
/// An orchestrator contains only macro steps, starts at its single initial step and is a chain of
/// stages: one transition per stage, an AND split to several macros and an AND join back.
/// OR branches, nested parallel branches and jumps back to anything but the initial step are rejected.
/// Transition conditions are ignored: the template always waits for the macros' Done.
/// </summary>
public static class OrchestratorContextBuilder
{
    public static IList<OrchestratorContext> Build(
        IList<FlowInfo> flows,
        IList<DeviceVariable> variables,
        IList<DeviceType> deviceTypes,
        IList<UnitInfo> units)
    {
        var flowById = flows
            .Where(flow => !string.IsNullOrWhiteSpace(flow.Id))
            .GroupBy(flow => flow.Id!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        return flows
            .Where(flow => flow.IsOrchestrator())
            .Select(flow => BuildOne(flow, flowById, variables, deviceTypes, units))
            .ToList();
    }

    private static OrchestratorContext BuildOne(
        FlowInfo flow,
        IDictionary<string, FlowInfo> flowById,
        IList<DeviceVariable> variables,
        IList<DeviceType> deviceTypes,
        IList<UnitInfo> units)
    {
        var name = flow.Name ?? flow.Id ?? string.Empty;
        var steps = flow.Steps ?? new List<Step>();
        if (steps.Count == 0) throw Error(name, "has no steps.");

        var nonMacro = steps.FirstOrDefault(step => !string.Equals(step.Kind, FlowConstants.MacroStepKind, StringComparison.OrdinalIgnoreCase));
        if (nonMacro is not null) throw Error(name, $"step {nonMacro.LabelOrId()} is not a macro step. An orchestrator may contain only macro steps.");

        var initialSteps = steps.Where(step => step.IsInitial).ToList();
        if (initialSteps.Count != 1) throw Error(name, $"must have exactly one initial step, found {initialSteps.Count}.");

        var macroByStepId = steps.ToDictionary(step => step.Id, step => BuildMacro(name, step, flowById, variables, deviceTypes), StringComparer.OrdinalIgnoreCase);
        var (stageSteps, loopsToStart) = WalkStages(name, flow, initialSteps[0]);

        var stages = stageSteps.Select((stage, index) => new OrchestratorStageContext
        {
            index = index,
            isFirst = index == 0,
            isLast = index == stageSteps.Count - 1,
            isParallel = stage.Count > 1,
            macros = stage.Select(step => macroByStepId[step.Id]).ToList(),
            nextMacros = (index + 1 < stageSteps.Count ? stageSteps[index + 1] : loopsToStart ? stageSteps[0] : new List<Step>())
                .Select(step => macroByStepId[step.Id])
                .ToList()
        }).ToList();

        var unitId = flow.Diagram?.UnitId ?? string.Empty;
        var unit = units.FirstOrDefault(item => string.Equals(item.Id, unitId, StringComparison.OrdinalIgnoreCase));

        return new OrchestratorContext
        {
            id = flow.Id ?? string.Empty,
            name = name,
            controlState = flow.ControlState ?? flow.Mode ?? string.Empty,
            unit = new OrchestratorUnitContext
            {
                id = unitId,
                name = unit?.Name ?? flow.Diagram?.Unit ?? string.Empty,
                label = unit?.Label ?? unit?.Name ?? flow.Diagram?.Unit ?? string.Empty
            },
            loopsToStart = loopsToStart,
            stages = stages,
            macros = stages.SelectMany(stage => stage.macros).ToList()
        };
    }

    private static (List<List<Step>> Stages, bool LoopsToStart) WalkStages(string name, FlowInfo flow, Step initial)
    {
        var steps = flow.Steps;
        var transitions = flow.Transitions ?? new List<Transition>();
        var stepById = steps.ToDictionary(step => step.Id, StringComparer.OrdinalIgnoreCase);
        var outgoing = steps.ToDictionary(step => step.Id, step => transitions.Where(t => t.FromStepIds.Contains(step.Id, StringComparer.OrdinalIgnoreCase)).ToList(), StringComparer.OrdinalIgnoreCase);
        var incoming = steps.ToDictionary(step => step.Id, step => transitions.Where(t => t.ToStepIds.Contains(step.Id, StringComparer.OrdinalIgnoreCase)).ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var step in steps)
        {
            if (outgoing[step.Id].Count > 1) throw Error(name, $"step {step.LabelOrId()} has {outgoing[step.Id].Count} outgoing transitions. OR branches are not supported; use the MacroPort Enable signal to skip a macro.");
            if (incoming[step.Id].Count > 1) throw Error(name, $"step {step.LabelOrId()} has {incoming[step.Id].Count} incoming transitions. OR branches are not supported.");
        }

        var stages = new List<List<Step>>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var current = new List<Step> { initial };
        visited.Add(initial.Id);

        while (true)
        {
            stages.Add(current);
            var stageTransitions = current.Select(step => outgoing[step.Id].FirstOrDefault()).ToList();
            if (stageTransitions.All(t => t is null)) return (stages, false);

            var transition = stageTransitions[0];
            if (transition is null || stageTransitions.Any(t => !ReferenceEquals(t, transition)))
            {
                throw Error(name, $"the parallel macros {Describe(current)} must all join into one transition. Nested or partial parallel branches are not supported.");
            }

            var fromIds = transition.FromStepIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!fromIds.SetEquals(current.Select(step => step.Id)))
            {
                throw Error(name, $"transition {TransitionName(transition)} joins steps that are not the parallel macros {Describe(current)}. Nested or partial parallel branches are not supported.");
            }

            var next = transition.ToStepIds
                .Select(id => stepById.TryGetValue(id, out var step) ? step : throw Error(name, $"transition {TransitionName(transition)} targets missing step {id}."))
                .ToList();
            if (next.Count == 0) throw Error(name, $"transition {TransitionName(transition)} has no target step.");

            if (next.Any(step => string.Equals(step.Id, initial.Id, StringComparison.OrdinalIgnoreCase)))
            {
                if (next.Count > 1) throw Error(name, $"transition {TransitionName(transition)} returns to the initial step together with other steps.");
                break;
            }

            var revisited = next.FirstOrDefault(step => visited.Contains(step.Id));
            if (revisited is not null) throw Error(name, $"transition {TransitionName(transition)} jumps back to step {revisited.LabelOrId()}. Only a return to the initial step is allowed.");

            foreach (var step in next) visited.Add(step.Id);
            current = next;
        }

        var unreachable = steps.FirstOrDefault(step => !visited.Contains(step.Id));
        if (unreachable is not null) throw Error(name, $"step {unreachable.LabelOrId()} is not reachable from the initial step.");

        return (stages, true);
    }

    private static OrchestratorMacroContext BuildMacro(
        string name,
        Step step,
        IDictionary<string, FlowInfo> flowById,
        IList<DeviceVariable> variables,
        IList<DeviceType> deviceTypes)
    {
        if (string.IsNullOrWhiteSpace(step.MacroFlowId)) throw Error(name, $"macro step {step.LabelOrId()} has no MacroStep selected.");
        if (!flowById.TryGetValue(step.MacroFlowId, out var target)) throw Error(name, $"macro step {step.LabelOrId()} references missing MacroStep flow {step.MacroFlowId}.");

        var targetName = target.Name ?? target.Id ?? string.Empty;
        if (!string.Equals(target.DiagramType ?? target.Diagram?.DiagramType, FlowConstants.MacroStepDiagramType, StringComparison.OrdinalIgnoreCase))
        {
            throw Error(name, $"macro step {step.LabelOrId()} references flow {targetName}, which is not a MacroStep.");
        }

        var port = target.MacroPortVariable
            ?? variables.FirstOrDefault(variable => string.Equals(variable.Label, targetName, StringComparison.OrdinalIgnoreCase));
        if (port is null)
        {
            throw Error(name, $"MacroStep {targetName} has no MacroPort variable. Add a variable named {targetName} with format {FlowConstants.MacroPortFormat}.");
        }
        if (!string.Equals(port.Format, FlowConstants.MacroPortFormat, StringComparison.OrdinalIgnoreCase))
        {
            throw Error(name, $"variable {port.Label} of MacroStep {targetName} has format {port.Format}, expected {FlowConstants.MacroPortFormat}.");
        }

        return new OrchestratorMacroContext
        {
            stepId = step.Id,
            stepNumber = step.Number,
            stepLabel = step.LabelOrId(),
            execAddress = step.ExecAddress ?? string.Empty,
            doneAddress = step.DoneAddress ?? string.Empty,
            macroStepId = target.Id ?? string.Empty,
            macroStep = targetName,
            port = new OrchestratorPortContext
            {
                label = port.Label,
                signals = MapPortSignals(port, deviceTypes)
            }
        };
    }

    /// <summary>Keys the port addresses by signal name; signalAddresses may be keyed by signal id.</summary>
    private static IDictionary<string, string> MapPortSignals(DeviceVariable port, IList<DeviceType> deviceTypes)
    {
        var deviceType = deviceTypes.FirstOrDefault(type => string.Equals(type.Name, port.Format, StringComparison.OrdinalIgnoreCase));
        if (deviceType is null) return new Dictionary<string, string>(port.SignalAddresses, StringComparer.OrdinalIgnoreCase);

        var signals = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var signal in deviceType.Signals)
        {
            var address = port.GetSignalAddress(signal.Id) ?? port.GetSignalAddress(signal.Name);
            if (!string.IsNullOrWhiteSpace(signal.Name) && !string.IsNullOrWhiteSpace(address)) signals[signal.Name] = address;
        }

        return signals;
    }

    private static string Describe(IEnumerable<Step> steps) => string.Join(", ", steps.Select(step => step.LabelOrId()));

    private static string TransitionName(Transition transition)
        => !string.IsNullOrWhiteSpace(transition.Label) ? transition.Label : transition.Id;

    private static InvalidOperationException Error(string orchestrator, string message)
        => new($"Orchestrator {orchestrator}: {message}");
}
