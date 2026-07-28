using GrafcetStudio.App.Generators.Keyence;
using GrafcetStudio.CodeGen.Runtime;
using GrafcetStudio.CodeGen.Runtime.Models;
using GrafcetStudio.Domain.Models;
using GrafcetStudio.Domain.Resolution;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace GrafcetStudio.App.Generators.Common;

/// <summary>
/// Builds a <see cref="GeneratorContext"/> from a <see cref="CodegenPayload"/>.
/// Platform-agnostic — can be used by any PLC generator (Keyence, Siemens, Mitsubishi, etc.).
/// </summary>
public static class GeneratorContextBuilder
{
    /// <summary>
    /// Builds the full generator context required by template-based PLC generators.
    /// </summary>
    public static GeneratorContext Build(CodegenPayload payload, ISequenceResolver sequenceResolver)
    {
        var unitId = payload.Unit?.Id ?? string.Empty;
        var unitLabel = !string.IsNullOrWhiteSpace(payload.Unit?.Label)
            ? payload.Unit!.Label!
            : !string.IsNullOrWhiteSpace(payload.Unit?.Name)
                ? payload.Unit!.Name!
                : payload.Project?.Name ?? "Unit";
        var flows = payload.Flows ?? new();
        var library = LoadDeviceLibrary(payload.DeviceLibraryPath);
        var macroBindings = AnalyzeMacroFlows(flows, payload.Variables);
        var macroPorts = macroBindings
            .Select(binding => new MacroPortContext
            {
                unitId = binding.unitId,
                callerFlowId = binding.callerFlowId,
                callerStepId = binding.callerStepId,
                calleeFlowId = binding.calleeFlowId,
                portName = binding.portName,
                variable = binding.variable
            })
            .ToList();
        var resolvedFlowResults = flows.Select(flow => BuildResolvedFlow(flow, payload.Variables, library, macroBindings, sequenceResolver)).ToList();
        var resolvedFlows = resolvedFlowResults.Select(result => result.Flow).ToList();
        var runtimePlans = resolvedFlowResults.Select(result => result.RuntimePlan).ToList();
        var outputBindings = DeviceOutputGroupBuilder.MergeOutputBindings(runtimePlans.SelectMany(plan => plan.OutputBindingPlan.Bindings));
        var unitVariable = DeviceOutputGroupBuilder.FindUnitVariable(payload.Variables, unitLabel);
        var unitAddresses = unitVariable?.SignalAddresses ?? new Dictionary<string, string>();
        var deviceOutputGroups = DeviceOutputGroupBuilder.BuildDeviceOutputGroups(outputBindings, payload.Variables, payload.DeviceTypes, unitAddresses);
        var unitStepRange = StepAddressHelper.BuildUnitStepAddressRange(resolvedFlows);
        var macroFlows = resolvedFlows.Where(f => string.Equals(f.diagramType, "Macro", StringComparison.OrdinalIgnoreCase)).ToList();
        var macroStepFlows = resolvedFlows.Where(f => string.Equals(f.diagramType, "MacroStep", StringComparison.OrdinalIgnoreCase)).ToList();
        var autoFlows = macroFlows.Where(f => string.Equals(f.normalizedType, "auto", StringComparison.OrdinalIgnoreCase)).ToList();
        var originFlows = macroFlows.Where(f => string.Equals(f.normalizedType, "origin", StringComparison.OrdinalIgnoreCase)).ToList();
        var deviceTypesByName = payload.DeviceTypes.ToDictionaryIgnoreCase(d => d.Name);
        var devices = payload.Variables.Select(variable =>
        {
            deviceTypesByName.TryGetValue(variable.Format, out var deviceType);
            var kind = NormalizeDeviceKind(variable.Format);
            return new DeviceContext
            {
                label = variable.Label,
                name = variable.Label,
                kind = kind,
                format = variable.Format,
                address = variable.Address,
                partialName = $"device_{kind}",
                standardPartialName = ResolveStandardDevicePartial(kind),
                signalAddresses = variable.SignalAddresses,
                signals = deviceType?.Signals.Select(signal => new DeviceSignalContext
                {
                    id = signal.Id,
                    name = signal.Name,
                    dataType = signal.DataType,
                    varType = signal.VarType.ToString(),
                    comment = signal.Comment,
                    address = variable.GetSignalAddress(signal.Id) ?? variable.GetSignalAddress(signal.Name)
                }).ToList()
            };
        }).ToList();

        return new GeneratorContext
        {
            project = payload.Project,
            unit = new UnitContext
            {
                id = unitId,
                label = unitLabel,
                unitIndex = 0,
                stepMinAddress = unitStepRange.MinAddress,
                stepMaxAddress = unitStepRange.MaxAddress,
                variable = devices.FirstOrDefault(d => d.name.Contains(unitLabel, StringComparison.OrdinalIgnoreCase))
            },
            devices = devices,
            flows = resolvedFlows,
            autoFlows = autoFlows,
            originFlows = originFlows,
            macroFlows = macroFlows,
            macroStepFlows = macroStepFlows,
            macroBindings = macroBindings,
            macroPorts = macroPorts,
            deviceOutputGroups = deviceOutputGroups,
            warnings = Array.Empty<string>()
        };
    }

    // -------------------------------------------------------------------------
    // Flow resolution
    // -------------------------------------------------------------------------

    private static ResolvedFlowBuildResult BuildResolvedFlow(
        FlowInfo flow,
        IList<DeviceVariable> variables,
        DeviceLibraryRoot library,
        IList<MacroBindingContext> macroBindings,
        ISequenceResolver sequenceResolver)
    {
        var state = flow.ToDiagramState(variables);
        var sequence = sequenceResolver.Resolve(state);
        var callerBindings = macroBindings.Where(binding => string.Equals(binding.callerFlowId, flow.Id, StringComparison.OrdinalIgnoreCase)).ToList();
        var calleeBindings = macroBindings.Where(binding => string.Equals(binding.calleeFlowId, flow.Id, StringComparison.OrdinalIgnoreCase)).ToList();
        var resolvedSteps = sequence.Select((entry, index) =>
        {
            var step = EnrichStepActions(entry.Step, variables, library);
            var previousStep = index > 0 ? sequence[index - 1].Step : null;
            var nextStep = index < sequence.Count - 1 ? sequence[index + 1].Step : null;
            var macroBinding = callerBindings.FirstOrDefault(binding => string.Equals(binding.callerStepId, step.Id, StringComparison.OrdinalIgnoreCase));

            return new ResolvedStep
            {
                Index = index,
                IsFirst = index == 0,
                Step = step,
                MacroBinding = macroBinding,
                PreviousStep = previousStep,
                NextStep = nextStep,
                InTransition = entry.InTransition,
                OutTransition = entry.OutTransition,
                BranchType = entry.BranchType
            };
        }).ToList();

        var flowStepRange = StepAddressHelper.BuildFlowStepAddressRange(flow);
        var instanceName = "ST_" + (flow.Name ?? string.Empty).Replace(" ", "_");
        var flowVar = flow.FlowVariable
            ?? variables.FirstOrDefault(v => string.Equals(v.Label, instanceName, StringComparison.OrdinalIgnoreCase));

        var resolvedFlow = new ResolvedFlow
        {
            id = flow.Id,
            name = flow.Name,
            type = flow.Type,
            mode = flow.Mode,
            normalizedType = NormalizeFlowType(flow),
            diagramType = NormalizeDiagramType(flow),
            diagram = flow.Diagram,
            macroPortVariable = flow.MacroPortVariable,
            flowVariable = flowVar,
            stepMinAddress = flowStepRange.MinAddress,
            stepMaxAddress = flowStepRange.MaxAddress,
            sequenceEnd = flowStepRange.SequenceEnd,
            steps = resolvedSteps,
            rawSteps = flow.Steps,
            transitions = flow.Transitions,
            macroBindings = macroBindings.Where(binding =>
                string.Equals(binding.callerFlowId, flow.Id, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(binding.calleeFlowId, flow.Id, StringComparison.OrdinalIgnoreCase)).ToList(),
            callerMacroBindings = callerBindings,
            calleeMacroBindings = calleeBindings
        };

        var runtimePlan = RuntimePlanBuilder.Build(new FlowInfo
        {
            Id = flow.Id,
            Name = flow.Name,
            Type = flow.Type,
            Mode = flow.Mode,
            DiagramType = flow.DiagramType,
            ControlState = flow.ControlState,
            Category = flow.Category,
            OrchestratorConfig = flow.OrchestratorConfig,
            Diagram = flow.Diagram,
            MacroPortVariable = flow.MacroPortVariable,
            Steps = resolvedSteps.Select(step => step.Step).ToList(),
            Transitions = flow.Transitions
        }, variables, library);

        return new ResolvedFlowBuildResult(resolvedFlow, runtimePlan);
    }

    private sealed record ResolvedFlowBuildResult(ResolvedFlow Flow, DiagramRuntimePlan RuntimePlan);

    // -------------------------------------------------------------------------
    // Step enrichment
    // -------------------------------------------------------------------------

    private static Step EnrichStepActions(Step step, IList<DeviceVariable> variables, DeviceLibraryRoot library)
    {
        if (string.IsNullOrWhiteSpace(step.ExecAddress) || step.Actions.Count == 0) return step;

        var enrichedActions = step.Actions.Select(action => EnrichStepAction(action, step.ExecAddress, variables, library)).ToList();
        return new Step
        {
            Id = step.Id,
            Number = step.Number,
            Label = step.Label,
            Kind = step.Kind,
            MacroFlowId = step.MacroFlowId,
            IsInitial = step.IsInitial,
            ExecAddress = step.ExecAddress,
            DoneAddress = step.DoneAddress,
            Actions = enrichedActions
        };
    }

    private static StepAction EnrichStepAction(
        StepAction action,
        string stepExecAddress,
        IList<DeviceVariable> variables,
        DeviceLibraryRoot library)
    {
        var resolved = DeviceCommandResolver.Resolve(action, stepExecAddress, variables, library);
        var feedback = resolved?.FeedbackSignals.FirstOrDefault(signal => !string.IsNullOrWhiteSpace(signal.PhysicalAddress));
        var completion = feedback is null
            ? action.Complete
            : new StepActionCompletion
            {
                Sensor = feedback.SignalName,
                SensorLabel = feedback.Label,
                Address = feedback.PhysicalAddress
            };

        return new StepAction
        {
            Variable = action.Variable,
            Address = action.Address,
            Qualifier = action.Qualifier,
            TimeMs = action.TimeMs,
            Complete = completion,
            SensorRef = feedback is null ? action.SensorRef : BuildSensorRef(action.Variable, feedback.SignalName),
            DeviceCommandResolutionAttempted = true,
            ResolvedCommand = resolved
        };
    }

    private static string? BuildSensorRef(string actionVariable, string sensorName)
    {
        var separatorIndex = actionVariable.IndexOf('.');
        if (separatorIndex <= 0 || string.IsNullOrWhiteSpace(sensorName)) return null;

        return $"{actionVariable[..separatorIndex]}.{sensorName}";
    }

    // -------------------------------------------------------------------------
    // Macro flow analysis
    // -------------------------------------------------------------------------

    private static IList<MacroBindingContext> AnalyzeMacroFlows(IList<FlowInfo> flows, IList<DeviceVariable> variables)
    {
        var flowById = flows
            .Where(flow => !string.IsNullOrWhiteSpace(flow.Id))
            .ToDictionary(flow => flow.Id!, StringComparer.OrdinalIgnoreCase);
        var macroPortVariablesByName = variables
            .Where(variable => !string.IsNullOrWhiteSpace(variable.Label))
            .GroupBy(variable => variable.Label, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
        var references = new Dictionary<string, List<(FlowInfo Caller, Step Step)>>(StringComparer.OrdinalIgnoreCase);
        var bindings = new List<MacroBindingContext>();

        foreach (var flow in flows)
        {
            var flowType = NormalizeDiagramType(flow);
            if (string.Equals(flowType, "MacroStep", StringComparison.OrdinalIgnoreCase))
            {
                ResolveAndValidateMacroPortVariable(flow, macroPortVariablesByName);
            }

            foreach (var step in flow.Steps ?? new List<Step>())
            {
                if (!string.Equals(step.Kind, "macro", StringComparison.OrdinalIgnoreCase)) continue;

                if (string.Equals(flowType, "MacroStep", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"MacroStep {flow.Name ?? flow.Id} cannot contain nested macro steps.");
                }

                if (string.IsNullOrWhiteSpace(step.MacroFlowId))
                {
                    throw new InvalidOperationException($"Step {step.LabelOrId()} is macro step but macroFlowId is empty.");
                }

                if (!flowById.TryGetValue(step.MacroFlowId!, out var target))
                {
                    throw new InvalidOperationException($"Step {step.LabelOrId()} references missing MacroStep flow: {step.MacroFlowId}.");
                }

                if (!string.Equals(NormalizeDiagramType(target), "MacroStep", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Step {step.LabelOrId()} references flow {target.Name ?? target.Id}, but target diagramType is not MacroStep.");
                }

                if (!string.Equals(ResolveFlowUnitId(flow), ResolveFlowUnitId(target), StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Step {step.LabelOrId()} references MacroStep {target.Name ?? target.Id} from another unit.");
                }

                ResolveAndValidateMacroPortVariable(target, macroPortVariablesByName);
                var targetId = target.Id ?? step.MacroFlowId!;
                if (!references.TryGetValue(targetId, out var callers))
                {
                    callers = new List<(FlowInfo Caller, Step Step)>();
                    references[targetId] = callers;
                }

                callers.Add((flow, step));
                bindings.Add(new MacroBindingContext
                {
                    unitId = ResolveFlowUnitId(flow),
                    callerFlowId = flow.Id ?? string.Empty,
                    callerStepId = step.Id,
                    calleeFlowId = target.Id ?? string.Empty,
                    portName = BuildMacroPortName(target),
                    variable = ResolveMacroPortVariable(target)
                });
            }
        }

        foreach (var pair in references)
        {
            if (pair.Value.Count > 1 && flowById.TryGetValue(pair.Key, out var target))
            {
                throw new InvalidOperationException($"MacroStep {target.Name ?? target.Id} is referenced by multiple macro steps.");
            }
        }

        return bindings;
    }

    private static void ResolveAndValidateMacroPortVariable(
        FlowInfo flow,
        IDictionary<string, List<DeviceVariable>> variablesByLabel)
    {
        var name = flow.Name ?? flow.Id ?? string.Empty;
        var matches = !string.IsNullOrWhiteSpace(name) && variablesByLabel.TryGetValue(name, out var found)
            ? found
            : new List<DeviceVariable>();

        if (matches.Count > 1)
        {
            throw new InvalidOperationException($"Duplicate MacroPort variable name for MacroStep {name}.");
        }

        if (matches.Count == 1 && !string.Equals(matches[0].Format, "MacroPort", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"MacroStep {name} has variable with same name but format/dataType/structure is {matches[0].Format}, expected MacroPort.");
        }

        if (flow.MacroPortVariable is null && matches.Count == 1)
        {
            flow.MacroPortVariable = matches[0];
        }

        if (flow.MacroPortVariable is not null && !string.Equals(flow.MacroPortVariable.Format, "MacroPort", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"MacroStep {name} macroPortVariable format is {flow.MacroPortVariable.Format}, expected MacroPort.");
        }
    }

    private static DeviceVariable? ResolveMacroPortVariable(FlowInfo flow)
        => flow.MacroPortVariable;

    private static string BuildMacroPortName(FlowInfo flow)
    {
        var source = !string.IsNullOrWhiteSpace(flow.Name) ? flow.Name! : flow.Id ?? "MacroStep";
        var token = new string(source.Trim().Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray()).Trim('_');
        return flow.MacroPortVariable?.Label ?? $"{(string.IsNullOrWhiteSpace(token) ? "MacroStep" : token)}_Port";
    }

    private static string ResolveFlowUnitId(FlowInfo flow)
        => flow.Diagram?.UnitId ?? string.Empty;

    // -------------------------------------------------------------------------
    // Normalization helpers
    // -------------------------------------------------------------------------

    private static string NormalizeFlowType(FlowInfo flow)
    {
        var value = flow.Type ?? flow.Mode ?? string.Empty;
        return string.Equals(value, "origin", StringComparison.OrdinalIgnoreCase) ? "origin" : "auto";
    }

    private static string NormalizeDiagramType(FlowInfo flow)
        => string.Equals(flow.DiagramType ?? flow.Diagram?.DiagramType, "MacroStep", StringComparison.OrdinalIgnoreCase)
            ? "MacroStep"
            : "Macro";

    private static string NormalizeDeviceKind(string? format)
    {
        if (string.IsNullOrWhiteSpace(format)) return "generic";
        return new string(format.Trim().ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray()).Trim('_');
    }

    /// <summary>
    /// Maps a normalized device kind to the standard Handlebars partial name used across generators.
    /// Override this mapping per-generator if needed.
    /// </summary>
    public static string ResolveStandardDevicePartial(string kind) => kind switch
    {
        "cylinder" => "uc.deviceCylinder",
        "servo" => "uc.deviceServo",
        "motor" => "uc.deviceMotor",
        _ => "uc.deviceGeneric"
    };

    // -------------------------------------------------------------------------
    // Device library
    // -------------------------------------------------------------------------

    private static DeviceLibraryRoot LoadDeviceLibrary(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return new DeviceLibraryRoot();

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<DeviceLibraryRoot>(json) ?? new DeviceLibraryRoot();
    }
}
