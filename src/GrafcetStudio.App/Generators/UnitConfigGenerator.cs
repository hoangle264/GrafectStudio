using GrafcetStudio.CodeGen.Runtime;
using GrafcetStudio.CodeGen.Runtime.Models;
using GrafcetStudio.CodeGen.Template;
using GrafcetStudio.Domain.Models;
using GrafcetStudio.Domain.Resolution;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace GrafcetStudio.App.Generators;

public class UnitConfigGenerator : ICodeGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly string[] SectionTemplateOrder =
    [
        "uc.error",
        "uc.manual",
        "uc.origin",
        "uc.auto"
    ];

    private static readonly (string TemplateId, string PartialName)[] KnownPartials =
    [
        ("uc.stepBody", "step_body"),
        ("uc.deviceCylinder", "device_cylinder"),
        ("uc.deviceServo", "device_servo"),
        ("uc.deviceMotor", "device_motor"),
        ("uc.deviceGeneric", "device_generic")
    ];
    private readonly TemplateManager _templates;
    private readonly ISequenceResolver _sequenceResolver;

    public UnitConfigGenerator(TemplateManager templates, ISequenceResolver sequenceResolver)
    {
        _templates = templates;
        _sequenceResolver = sequenceResolver;
    }

    public string Platform => "unit-config";
    public string Generate(CodegenPayload payload)
    {
         var context = BuildContext(payload);
        RegisterPartials();

        var renderedSections = ResolveSectionTemplateNames()
            .Select(templateName => _templates.TryRender(templateName, context, out var result) ? result : string.Empty)
            .Where(section => !string.IsNullOrWhiteSpace(section))
            .ToList();

        return renderedSections.Count == 0
            ? JsonSerializer.Serialize(context, JsonOptions)
            : string.Join(Environment.NewLine, renderedSections);
    }

    private IEnumerable<string> ResolveSectionTemplateNames()
    {
        foreach (var templateName in SectionTemplateOrder)
        {
            if (_templates.IsTemplateLoaded(templateName)) yield return templateName;
        }

        if (_templates.IsTemplateLoaded("uc.mainOutput"))
        {
            yield return "uc.mainOutput";
        }
        else if (_templates.IsTemplateLoaded("uc.outputLegacy"))
        {
            yield return "uc.outputLegacy";
        }
    }

    private void RegisterPartials()
    {
        foreach (var (templateId, partialName) in KnownPartials)
        {
            RegisterPartialIfLoaded(templateId, partialName);
        }

        foreach (var templateId in _templates.GetLoadedTemplateIds().Where(id => id.StartsWith("device_", StringComparison.OrdinalIgnoreCase)))
        {
            RegisterPartialIfLoaded(templateId, templateId);
        }
    }

    private void RegisterPartialIfLoaded(string templateId, string partialName)
    {
        if (!_templates.IsTemplateLoaded(templateId) || _templates.IsPartialRegistered(partialName)) return;

        var source = _templates.GetTemplateSource(templateId);
        if (!string.IsNullOrEmpty(source)) _templates.RegisterPartial(partialName, source);
    }

    private object BuildContext(CodegenPayload payload)
    {
        var unitId = payload.Unit?.Id ?? string.Empty;
        var unitLabel = !string.IsNullOrWhiteSpace(payload.Unit?.Label)
            ? payload.Unit!.Label!
            : !string.IsNullOrWhiteSpace(payload.Unit?.Name)
                ? payload.Unit!.Name!
                : payload.Project?.Name ?? "Unit";
        var flows = payload.Flows ?? new();
        var library = LoadDeviceLibrary(payload.DeviceLibraryPath);
        var resolvedFlows = flows.Select(flow => BuildResolvedFlow(flow, payload.Variables, library)).ToList();
        var runtimePlans = flows.Select(flow => RuntimePlanBuilder.Build(flow, payload.Variables, library)).ToList();
        var outputBindings = MergeOutputBindings(runtimePlans.SelectMany(plan => plan.OutputBindingPlan.Bindings));
        var unitVariable = FindUnitVariable(payload.Variables, unitLabel);
        var unitAddresses = unitVariable?.SignalAddresses ?? new Dictionary<string, string>();
        var deviceOutputGroups = BuildDeviceOutputGroups(outputBindings, payload.Variables, payload.DeviceTypes, unitAddresses);
        var autoFlows = resolvedFlows.Where(f => string.Equals(f.normalizedType, "auto", StringComparison.OrdinalIgnoreCase)).ToList();
        var originFlows = resolvedFlows.Where(f => string.Equals(f.normalizedType, "origin", StringComparison.OrdinalIgnoreCase)).ToList();

        var deviceTypesByName = payload.DeviceTypes.ToDictionary(d => d.Name, StringComparer.OrdinalIgnoreCase);
        var devices = payload.Variables.Select(variable =>
        {
            deviceTypesByName.TryGetValue(variable.Format, out var deviceType);
            var kind = NormalizeDeviceKind(variable.Format);
            return new
            {
                label = variable.Label,
                name = variable.Label,
                kind,
                format = variable.Format,
                address = variable.Address,
                partialName = $"device_{kind}",
                standardPartialName = ResolveStandardDevicePartial(kind),
                signalAddresses = variable.SignalAddresses,
                signals = deviceType?.Signals.Select(signal => new
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

        return new
        {
            project = payload.Project,
            unit = new
            {
                id = unitId,
                label = unitLabel,
                unitIndex = 0,
                variable = devices.FirstOrDefault(d => d.name.Contains(unitLabel, StringComparison.OrdinalIgnoreCase))
            },
            devices,
            autoFlows,
            originFlows,
            //outputBindings,
            deviceOutputGroups,
            warnings = Array.Empty<string>()
        };
    }




    private ResolvedFlow BuildResolvedFlow(FlowInfo flow, IList<DeviceVariable> variables, DeviceLibraryRoot library)
    {
        var state = flow.ToDiagramState(variables);
        var sequence = _sequenceResolver.Resolve(state);
        var resolvedSteps = sequence.Select((entry, index) => new ResolvedStep
        {
            Index = index,
            IsFirst = index == 0,
            Step = EnrichStepActions(entry.Step, variables, library),
            PreviousStep = index > 0 ? sequence[index - 1].Step : null,
            NextStep = index < sequence.Count - 1 ? sequence[index + 1].Step : null,
            InTransition = entry.InTransition,
            OutTransition = entry.OutTransition,
            BranchType = entry.BranchType
        }).ToList();

        return new ResolvedFlow
        {
            id = flow.Id,
            name = flow.Name,
            type = flow.Type,
            mode = flow.Mode,
            normalizedType = NormalizeFlowType(flow),
            diagram = flow.Diagram,
            steps = resolvedSteps,
            rawSteps = flow.Steps,
            transitions = flow.Transitions
        };
    }


    private static Step EnrichStepActions(Step step, IList<DeviceVariable> variables, DeviceLibraryRoot library)
    {
        if (string.IsNullOrWhiteSpace(step.ExecAddress) || step.Actions.Count == 0) return step;

        var enrichedActions = step.Actions.Select(action => EnrichStepAction(action, step.ExecAddress, variables, library)).ToList();
        return new Step
        {
            Id = step.Id,
            Number = step.Number,
            Label = step.Label,
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
        if (feedback is null) return action;

        return new StepAction
        {
            Variable = action.Variable,
            Address = action.Address,
            Qualifier = action.Qualifier,
            TimeMs = action.TimeMs,
            Complete = new StepActionCompletion
            {
                Sensor = feedback.SignalName,
                SensorLabel = feedback.Label,
                Address = feedback.PhysicalAddress
            },
            SensorRef = BuildSensorRef(action.Variable, feedback.SignalName)
        };
    }

    private static string? BuildSensorRef(string actionVariable, string sensorName)
    {
        var separatorIndex = actionVariable.IndexOf('.');
        if (separatorIndex <= 0 || string.IsNullOrWhiteSpace(sensorName)) return null;

        return $"{actionVariable[..separatorIndex]}.{sensorName}";
    }

    private static IList<DeviceOutputGroup> BuildDeviceOutputGroups(
        IList<AggregatedOutputBinding> mergedBindings,
        IList<DeviceVariable> variables,
        IList<DeviceType> deviceTypes,
        IDictionary<string, string> unitAddresses)
    {
        var variablesByLabel = variables.ToDictionary(variable => variable.Label, StringComparer.OrdinalIgnoreCase);
        var deviceTypesByName = deviceTypes.ToDictionary(deviceType => deviceType.Name, StringComparer.OrdinalIgnoreCase);
        var flattenedSources = mergedBindings
            .SelectMany(binding => binding.Sources.Select(source => new { Binding = binding, Source = source }))
            .Where(item => !string.IsNullOrWhiteSpace(item.Source.DeviceLabel))
            .ToList();

        return flattenedSources
            .GroupBy(item => item.Source.DeviceLabel, StringComparer.OrdinalIgnoreCase)
            .Select(deviceGroup =>
            {
                var firstSource = deviceGroup.Select(item => item.Source).First();
                variablesByLabel.TryGetValue(deviceGroup.Key, out var variable);
                deviceTypesByName.TryGetValue(firstSource.DeviceFormat, out var deviceType);

                var signals = variable is null || deviceType is null
                    ? new List<object>()
                    : deviceType.Signals.Select(signal => new
                    {
                        name = signal.Name,
                        dataType = signal.DataType,
                        varType = signal.VarType.ToString(),
                        comment = signal.Comment,
                        address = variable.GetSignalAddress(signal.Id) ?? variable.GetSignalAddress(signal.Name)
                    }).Cast<object>().ToList();

                var commands = deviceGroup
                    .Where(item => !string.IsNullOrWhiteSpace(item.Source.CommandId))
                    .GroupBy(item => item.Source.CommandId, StringComparer.OrdinalIgnoreCase)
                    .Select(commandGroup =>
                    {
                        var commandSource = commandGroup.Select(item => item.Source).First();
                        var commandBindings = commandGroup.Select(item => item.Binding);

                        return new DeviceCommandOutput
                        {
                            CommandId = commandSource.CommandId,
                            ActionLabel = commandSource.ActionLabel,
                            DriveSignal = commandSource.DriveSignal,
                            PhysicalOutputRef = commandGroup
                                .Select(item => item.Binding.PhysicalOutputRef)
                                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty,
                            AggregationMode = commandGroup
                                .Select(item => item.Binding.AggregationMode)
                                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "OR",
                            SourceSteps = commandBindings
                                .SelectMany(binding => binding.SourceSteps)
                                .Where(value => !string.IsNullOrWhiteSpace(value))
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .ToList(),
                            SourceExecuteBitRefs = commandBindings
                                .SelectMany(binding => binding.SourceExecuteBitRefs)
                                .Where(value => !string.IsNullOrWhiteSpace(value))
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .ToList(),
                            SourceDoneBitRefs = commandBindings
                                .SelectMany(binding => binding.SourceDoneBitRefs)
                                .Where(value => !string.IsNullOrWhiteSpace(value))
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .ToList(),
                            OriginFlows = BuildCommandFlowOutputs(commandGroup.Select(item => item.Source), "origin"),
                            AutoFlows = BuildCommandFlowOutputs(commandGroup.Select(item => item.Source), "auto"),
                            FeedbackSignals = commandGroup
                                .SelectMany(item => item.Source.FeedbackSignals)
                                .GroupBy(signal => $"{signal.SignalName}\u001F{signal.PhysicalAddress}", StringComparer.OrdinalIgnoreCase)
                                .Select(signalGroup => signalGroup.First())
                                .ToList()
                        };
                    })
                    .OrderBy(command => command.CommandId, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return new DeviceOutputGroup
                {
                    DeviceLabel = deviceGroup.Key,
                    DeviceFormat = firstSource.DeviceFormat,
                    DeviceKind = NormalizeDeviceKind(firstSource.DeviceFormat),
                    Address = variable?.Address,
                    SignalAddresses = variable?.SignalAddresses ?? new Dictionary<string, string>(),
                    UnitAddresses = new Dictionary<string, string>(unitAddresses, StringComparer.OrdinalIgnoreCase),
                    Signals = signals,
                    Commands = commands
                };
            })
            .OrderBy(group => group.DeviceLabel, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static DeviceVariable? FindUnitVariable(IList<DeviceVariable> variables, string unitLabel)
    {
        if (string.IsNullOrWhiteSpace(unitLabel)) return null;

        return variables.FirstOrDefault(variable => string.Equals(variable.Label, unitLabel, StringComparison.OrdinalIgnoreCase))
            ?? variables.FirstOrDefault(variable => variable.Label.Contains(unitLabel, StringComparison.OrdinalIgnoreCase));
    }


    private static IList<DeviceCommandFlowOutput> BuildCommandFlowOutputs(IEnumerable<OutputBindingSource> sources, string flowType)
    {
        return sources
            .Where(source => string.Equals(source.FlowType, flowType, StringComparison.OrdinalIgnoreCase))
            .GroupBy(source => $"{source.FlowId}\u001F{source.FlowName}", StringComparer.OrdinalIgnoreCase)
            .Select(flowGroup => new DeviceCommandFlowOutput
            {
                Id = flowGroup.First().FlowId,
                Name = flowGroup.First().FlowName,
                Commands = flowGroup
                    .Where(source => !string.IsNullOrWhiteSpace(source.SourceExecuteBitRef) || !string.IsNullOrWhiteSpace(source.SourceDoneBitRef))
                    .GroupBy(source => $"{source.CommandId}\u001F{source.ActionLabel}\u001F{source.SourceStep}\u001F{source.SourceExecuteBitRef}\u001F{source.SourceDoneBitRef}", StringComparer.OrdinalIgnoreCase)
                    .Select(commandGroup =>
                    {
                        var source = commandGroup.First();
                        return new DeviceCommandFlowBitOutput
                        {
                            CommandId = source.CommandId,
                            ActionLabel = source.ActionLabel,
                            SourceStep = source.SourceStep,
                            SourceExecuteBit = source.SourceExecuteBitRef,
                            SourceDoneBit = source.SourceDoneBitRef
                        };
                    })
                    .OrderBy(command => command.CommandId, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(command => command.SourceExecuteBit, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            })
            .Where(flow => flow.Commands.Count > 0)
            .OrderBy(flow => flow.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(flow => flow.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
    private static IList<AggregatedOutputBinding> MergeOutputBindings(IEnumerable<AggregatedOutputBinding> bindings)
    {
        return bindings
            .GroupBy(binding => binding.PhysicalOutputRef, StringComparer.OrdinalIgnoreCase)
            .Select(group => new AggregatedOutputBinding
            {
                PhysicalOutputRef = group.Key,
                SourceExecuteBitRefs = group
                    .SelectMany(binding => binding.SourceExecuteBitRefs)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                SourceDoneBitRefs = group
                    .SelectMany(binding => binding.SourceDoneBitRefs)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                SourceSteps = group
                    .SelectMany(binding => binding.SourceSteps)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                AggregationMode = group.Select(binding => binding.AggregationMode).FirstOrDefault(mode => !string.IsNullOrWhiteSpace(mode)) ?? "OR",
                Sources = group
                    .SelectMany(binding => binding.Sources)
                    .GroupBy(source => new
                    {
                        Flow = source.FlowId.ToUpperInvariant(),
                        Type = source.FlowType.ToUpperInvariant(),
                        Source = source.SourceExecuteBitRef.ToUpperInvariant(),
                        Done = source.SourceDoneBitRef.ToUpperInvariant(),
                        Action = source.ActionSymbol.ToUpperInvariant(),
                        Command = source.CommandId.ToUpperInvariant()
                    })
                    .Select(sourceGroup => sourceGroup.First())
                    .ToList()
            })
            .OrderBy(binding => binding.PhysicalOutputRef, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
    private static DeviceLibraryRoot LoadDeviceLibrary(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return new DeviceLibraryRoot();

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<DeviceLibraryRoot>(json) ?? new DeviceLibraryRoot();
    }

    private static string NormalizeFlowType(FlowInfo flow)
    {
        var value = flow.Type ?? flow.Mode ?? string.Empty;
        return string.Equals(value, "origin", StringComparison.OrdinalIgnoreCase) ? "origin" : "auto";
    }

    private static string NormalizeDeviceKind(string? format)
    {
        if (string.IsNullOrWhiteSpace(format)) return "generic";
        return new string(format.Trim().ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray()).Trim('_');
    }

    private static string ResolveStandardDevicePartial(string kind) => kind switch
    {
        "cylinder" => "uc.deviceCylinder",
        "servo" => "uc.deviceServo",
        "motor" => "uc.deviceMotor",
        _ => "uc.deviceGeneric"
    };
}








