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

public class KeyenceGenerator : LegacyCodeGeneratorBase
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

    public KeyenceGenerator(TemplateManager templates, ISequenceResolver sequenceResolver)
    {
        _templates = templates;
        _sequenceResolver = sequenceResolver;
    }

    public override string Platform => "unit-config";
    public string GenerateUnitContent(CodegenPayload payload) => GenerateLegacy(payload);
    protected override string GenerateLegacy(CodegenPayload payload)
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
        ValidateMacroStepRules(flows);
        ValidateMacroPortVariables(flows, payload.Variables);
        var macroBindings = BuildMacroBindings(flows);
        var macroPorts = macroBindings
            .Select(binding => new
            {
                binding.unitId,
                binding.callerFlowId,
                binding.callerStepId,
                binding.calleeFlowId,
                binding.portName,
                binding.variable
            })
            .ToList();
        var resolvedFlows = flows.Select(flow => BuildResolvedFlow(flow, payload.Variables, library, macroBindings)).ToList();
        var runtimePlans = flows.Select(flow => RuntimePlanBuilder.Build(flow, payload.Variables, library)).ToList();
        var outputBindings = MergeOutputBindings(runtimePlans.SelectMany(plan => plan.OutputBindingPlan.Bindings));
        var unitVariable = FindUnitVariable(payload.Variables, unitLabel);
        var unitAddresses = unitVariable?.SignalAddresses ?? new Dictionary<string, string>();
        var deviceOutputGroups = BuildDeviceOutputGroups(outputBindings, payload.Variables, payload.DeviceTypes, unitAddresses);
        var unitStepRange = BuildUnitStepAddressRange(resolvedFlows);
        var macroFlows = resolvedFlows.Where(f => string.Equals(f.diagramType, "Macro", StringComparison.OrdinalIgnoreCase)).ToList();
        var macroStepFlows = resolvedFlows.Where(f => string.Equals(f.diagramType, "MacroStep", StringComparison.OrdinalIgnoreCase)).ToList();
        var autoFlows = macroFlows.Where(f => string.Equals(f.normalizedType, "auto", StringComparison.OrdinalIgnoreCase)).ToList();
        var originFlows = macroFlows.Where(f => string.Equals(f.normalizedType, "origin", StringComparison.OrdinalIgnoreCase)).ToList();

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
                stepMinAddress = unitStepRange.MinAddress,
                stepMaxAddress = unitStepRange.MaxAddress,
                variable = devices.FirstOrDefault(d => d.name.Contains(unitLabel, StringComparison.OrdinalIgnoreCase))
            },
            devices,
            autoFlows,
            originFlows,
            macroFlows,
            macroStepFlows,
            macroBindings,
            macroPorts,
            //outputBindings,
            deviceOutputGroups,
            warnings = Array.Empty<string>()
        };
    }




    private ResolvedFlow BuildResolvedFlow(FlowInfo flow, IList<DeviceVariable> variables, DeviceLibraryRoot library, IList<MacroBindingContext> macroBindings)
    {
        var state = flow.ToDiagramState(variables);
        var sequence = _sequenceResolver.Resolve(state);
        var callerBindings = macroBindings.Where(binding => string.Equals(binding.callerFlowId, flow.Id, StringComparison.OrdinalIgnoreCase)).ToList();
        var calleeBindings = macroBindings.Where(binding => string.Equals(binding.calleeFlowId, flow.Id, StringComparison.OrdinalIgnoreCase)).ToList();
        var resolvedSteps = sequence.Select((entry, index) => new ResolvedStep
        {
            Index = index,
            IsFirst = index == 0,
            Step = EnrichStepActions(entry.Step, variables, library),
            MacroBinding = callerBindings.FirstOrDefault(binding => string.Equals(binding.callerStepId, entry.Step.Id, StringComparison.OrdinalIgnoreCase)),
            PreviousStep = index > 0 ? sequence[index - 1].Step : null,
            NextStep = index < sequence.Count - 1 ? sequence[index + 1].Step : null,
            InTransition = entry.InTransition,
            OutTransition = entry.OutTransition,
            BranchType = entry.BranchType
        }).ToList();
        var flowStepRange = BuildFlowStepAddressRange(flow);

        return new ResolvedFlow
        {
            id = flow.Id,
            name = flow.Name,
            type = flow.Type,
            mode = flow.Mode,
            normalizedType = NormalizeFlowType(flow),
            diagramType = NormalizeDiagramType(flow),
            diagram = flow.Diagram,
            macroPortVariable = flow.MacroPortVariable,
            stepMinAddress = flowStepRange.MinAddress,
            stepMaxAddress = flowStepRange.MaxAddress,
            sequenceEnd = flowStepRange.SequenceEnd,
            steps = resolvedSteps,
            rawSteps = flow.Steps,
            transitions = flow.Transitions,
            macroBindings = macroBindings.Where(binding => string.Equals(binding.callerFlowId, flow.Id, StringComparison.OrdinalIgnoreCase) || string.Equals(binding.calleeFlowId, flow.Id, StringComparison.OrdinalIgnoreCase)).ToList(),
            callerMacroBindings = callerBindings,
            calleeMacroBindings = calleeBindings
        };
    }

    private static (string MinAddress, string MaxAddress, string SequenceEnd) BuildFlowStepAddressRange(FlowInfo flow)
    {
        var addressedSteps = new List<(Step Step, StepExecAddress Address)>();
        foreach (var step in flow.Steps)
        {
            if (TryParseStepExecAddress(step.ExecAddress, flow.Diagram, out var parsed))
            {
                addressedSteps.Add((step, parsed));
            }
        }

        if (addressedSteps.Count == 0) return (string.Empty, string.Empty, string.Empty);

        var min = addressedSteps.OrderBy(item => item.Address.SortValue).First();
        var max = addressedSteps.OrderByDescending(item => item.Address.SortValue).First();
        var sequenceEnd = ResolveSequenceEnd(max.Step, max.Address, flow.Diagram);

        return (min.Step.ExecAddress ?? string.Empty, max.Step.ExecAddress ?? string.Empty, sequenceEnd);
    }

    private static (string MinAddress, string MaxAddress) BuildUnitStepAddressRange(IList<ResolvedFlow> flows)
    {
        var addresses = new List<(Step Step, StepExecAddress Address)>();
        foreach (var flow in flows)
        {
            foreach (var step in flow.rawSteps)
            {
                if (TryParseStepExecAddress(step.ExecAddress, flow.diagram, out var parsed))
                {
                    addresses.Add((step, parsed));
                }
            }
        }

        if (addresses.Count == 0) return (string.Empty, string.Empty);

        var min = addresses.OrderBy(item => item.Address.SortValue).First();
        var max = addresses.OrderByDescending(item => item.Address.SortValue).First();
        return (min.Step.ExecAddress ?? string.Empty, max.Step.ExecAddress ?? string.Empty);
    }

    private static string ResolveSequenceEnd(Step step, StepExecAddress parsedAddress, DiagramInfo? diagram)
    {
        var stepNumber = step.Number;
        if (stepNumber < 1) return IncrementParsedAddress(parsedAddress);

        if (string.Equals(diagram?.AddressMode, "word", StringComparison.OrdinalIgnoreCase))
        {
            return FormatWordStepExecAddress(diagram?.ActiveWord, stepNumber + 1);
        }

        var parsedBase = TryParseAddressBase(diagram?.BaseMr, out var configuredBase)
            ? configuredBase
            : ResolveBoolBase(parsedAddress, stepNumber, diagram?.BoolAddressMode);
        var offset = stepNumber * 2;
        var nextNumber = ResolveBoolMr(parsedBase.Number, offset, diagram?.BoolAddressMode);
        return FormatAddressBase(parsedBase.Prefix, nextNumber, parsedBase.Width);
    }
    private static ParsedBoolBase ResolveBoolBase(StepExecAddress parsedAddress, int stepNumber, string? boolAddressMode)
    {
        var offset = Math.Max(0, (stepNumber - 1) * 2);
        if (string.Equals(boolAddressMode, "block", StringComparison.OrdinalIgnoreCase))
        {
            return new ParsedBoolBase(parsedAddress.Prefix, parsedAddress.Number - (offset / 8) * 100 - (offset % 8), 0);
        }

        return new ParsedBoolBase(parsedAddress.Prefix, parsedAddress.Number - offset, 0);
    }

    private static int ResolveBoolMr(int baseMr, int offset, string? boolAddressMode)
    {
        return string.Equals(boolAddressMode, "block", StringComparison.OrdinalIgnoreCase)
            ? baseMr + (offset / 8) * 100 + offset % 8
            : baseMr + offset;
    }

    private static string FormatWordStepExecAddress(string? activeWord, int stepNumber)
    {
        if (stepNumber < 1) stepNumber = 1;

        var bitIndex = stepNumber - 1;
        var wordOffset = bitIndex / 16;
        var bit = bitIndex % 16;
        var word = FormatWordAddress(activeWord, wordOffset);
        return $"{word}.{bit}";
    }

    private static string FormatWordAddress(string? baseWord, int offset)
    {
        var value = string.IsNullOrWhiteSpace(baseWord) ? "DM0" : baseWord.Trim().TrimStart('@');
        var prefixLength = value.TakeWhile(char.IsLetter).Count();
        var prefix = prefixLength > 0 ? value[..prefixLength].ToUpperInvariant() : "DM";
        var numberText = value[prefixLength..];
        var number = int.TryParse(numberText, out var parsed) ? parsed : 0;
        var width = numberText.Length > 1 ? numberText.Length : 0;
        var nextNumber = number + offset;
        return width > 0 ? $"{prefix}{nextNumber.ToString().PadLeft(width, '0')}" : $"{prefix}{nextNumber}";
    }

    private static string IncrementParsedAddress(StepExecAddress parsedAddress)
        => parsedAddress.HasBit
            ? $"{parsedAddress.Prefix}{parsedAddress.Number}.{parsedAddress.Bit + 1}"
            : $"{parsedAddress.Prefix}{parsedAddress.Number + 1}";

    private static bool TryParseAddressBase(string? value, out ParsedBoolBase parsed)
    {
        parsed = default;
        var text = (value ?? string.Empty).Trim().TrimStart('@');
        if (string.IsNullOrWhiteSpace(text)) return false;
        var prefixLength = text.TakeWhile(char.IsLetter).Count();
        if (prefixLength <= 0 || prefixLength >= text.Length) return false;
        var prefix = text[..prefixLength].ToUpperInvariant();
        var numberText = text[prefixLength..];
        if (!int.TryParse(numberText, out var number)) return false;
        parsed = new ParsedBoolBase(prefix, number, numberText.Length > 1 ? numberText.Length : 0);
        return true;
    }

    private static string FormatAddressBase(string prefix, int number, int width)
        => width > 0 ? $"{prefix}{number.ToString().PadLeft(width, '0')}" : $"{prefix}{number}";

    private static bool TryParseStepExecAddress(string? address, DiagramInfo? diagram, out StepExecAddress parsed)
    {
        parsed = default;
        var value = (address ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(value)) return false;

        var trimmed = value.TrimStart('@');
        var dotIndex = trimmed.IndexOf('.');
        var head = dotIndex >= 0 ? trimmed[..dotIndex] : trimmed;
        var bitText = dotIndex >= 0 ? trimmed[(dotIndex + 1)..] : string.Empty;
        var prefixLength = head.TakeWhile(char.IsLetter).Count();
        if (prefixLength <= 0 || prefixLength >= head.Length) return false;

        var prefix = head[..prefixLength].ToUpperInvariant();
        if (!int.TryParse(head[prefixLength..], out var number)) return false;
        var hasBit = int.TryParse(bitText, out var bit);
        var sortValue = ResolveAddressSortValue(prefix, number, hasBit ? bit : 0, diagram);
        parsed = new StepExecAddress(prefix, number, hasBit, hasBit ? bit : 0, sortValue);
        return true;
    }

    private static long ResolveAddressSortValue(string prefix, int number, int bit, DiagramInfo? diagram)
    {
        if (string.Equals(prefix, "MR", StringComparison.OrdinalIgnoreCase))
        {
            if (TryParseAddressBase(diagram?.BaseMr, out var baseMr) && string.Equals(diagram.BoolAddressMode, "block", StringComparison.OrdinalIgnoreCase))
            {
                var relative = number - baseMr.Number;
                if (relative >= 0)
                {
                    return ((long)(relative / 100) * 8) + relative % 100;
                }
            }

            return number;
        }

        return ((long)number * 16) + bit;
    }

    private readonly record struct ParsedBoolBase(string Prefix, int Number, int Width);

    private readonly record struct StepExecAddress(string Prefix, int Number, bool HasBit, int Bit, long SortValue);


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

                        var flowCommands = BuildCommandFlowOutputs(commandGroup.Select(item => item.Source));
                        var originCommandCount = flowCommands.Count(command => command.IsOrigin);
                        var autoCommandCount = flowCommands.Count(command => command.IsAuto);

                        return new DeviceCommandOutput
                        {
                            CommandId = commandSource.CommandId,
                            ActionLabel = commandSource.ActionLabel,
                            DriveSignal = commandSource.DriveSignal,
                            InterlockSignal = commandGroup
                                .Select(item => item.Source.InterlockSignal)
                                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty,
                            InterlockAddress = commandGroup
                                .Select(item => item.Source.InterlockAddress)
                                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty,
                            InterlockLabel = commandGroup
                                .Select(item => item.Source.InterlockLabel)
                                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty,
                            InterlockRequiredState = commandGroup
                                .Select(item => item.Source.InterlockRequiredState)
                                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty,
                            HasInterlock = commandGroup.Any(item =>
                                !string.IsNullOrWhiteSpace(item.Source.InterlockSignal)
                                || !string.IsNullOrWhiteSpace(item.Source.InterlockAddress)),
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
                            FlowCommands = flowCommands,
                            FlowCommandCount = flowCommands.Count,
                            OriginCommandCount = originCommandCount,
                            AutoCommandCount = autoCommandCount,
                            HasOriginCommands = originCommandCount > 0,
                            HasAutoCommands = autoCommandCount > 0,
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


    private static IList<DeviceCommandFlowOutput> BuildCommandFlowOutputs(IEnumerable<OutputBindingSource> sources)
    {
        var commands = sources
            .Where(source => !string.IsNullOrWhiteSpace(source.SourceExecuteBitRef) || !string.IsNullOrWhiteSpace(source.SourceDoneBitRef))
            .GroupBy(source => $"{source.FlowType}\u001F{source.FlowId}\u001F{source.FlowName}\u001F{source.CommandId}\u001F{source.ActionLabel}\u001F{source.SourceStep}\u001F{source.SourceExecuteBitRef}\u001F{source.SourceDoneBitRef}", StringComparer.OrdinalIgnoreCase)
            .Select(commandGroup =>
            {
                var source = commandGroup.First();
                var flowType = string.Equals(source.FlowType, "origin", StringComparison.OrdinalIgnoreCase) ? "origin" : "auto";

                return new DeviceCommandFlowOutput
                {
                    Id = source.FlowId,
                    Name = source.FlowName,
                    FlowType = flowType,
                    IsOrigin = string.Equals(flowType, "origin", StringComparison.OrdinalIgnoreCase),
                    IsAuto = string.Equals(flowType, "auto", StringComparison.OrdinalIgnoreCase),
                    CommandId = source.CommandId,
                    ActionLabel = source.ActionLabel,
                    SourceStep = source.SourceStep,
                    SourceExecuteBit = source.SourceExecuteBitRef,
                    SourceDoneBit = source.SourceDoneBitRef
                };
            })
            .OrderBy(command => command.IsAuto)
            .ThenBy(command => command.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(command => command.Id, StringComparer.OrdinalIgnoreCase)
            .ThenBy(command => command.CommandId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(command => command.SourceExecuteBit, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return commands
            .Select((command, index) => new DeviceCommandFlowOutput
            {
                Id = command.Id,
                Name = command.Name,
                FlowType = command.FlowType,
                IsOrigin = command.IsOrigin,
                IsAuto = command.IsAuto,
                CommandId = command.CommandId,
                ActionLabel = command.ActionLabel,
                SourceStep = command.SourceStep,
                SourceExecuteBit = command.SourceExecuteBit,
                SourceDoneBit = command.SourceDoneBit,
                Index = index,
                Number = index + 1,
                TotalCount = commands.Count,
                IsFirst = index == 0,
                IsLast = index == commands.Count - 1,
                IsSingle = commands.Count == 1
            })
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
                        Command = source.CommandId.ToUpperInvariant(),
                        Interlock = source.InterlockSignal.ToUpperInvariant()
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

    private static string NormalizeDiagramType(FlowInfo flow)
        => string.Equals(flow.DiagramType ?? flow.Diagram?.DiagramType, "MacroStep", StringComparison.OrdinalIgnoreCase)
            ? "MacroStep"
            : "Macro";

    private static string ResolveFlowUnitId(FlowInfo flow)
        => flow.Diagram?.UnitId ?? string.Empty;

    private static void ValidateMacroStepRules(IList<FlowInfo> flows)
    {
        var flowById = flows
            .Where(flow => !string.IsNullOrWhiteSpace(flow.Id))
            .ToDictionary(flow => flow.Id!, StringComparer.OrdinalIgnoreCase);
        var references = new Dictionary<string, List<(FlowInfo Caller, Step Step)>>(StringComparer.OrdinalIgnoreCase);

        foreach (var flow in flows)
        {
            var flowType = NormalizeDiagramType(flow);
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

                if (!references.TryGetValue(target.Id ?? step.MacroFlowId!, out var callers))
                {
                    callers = new List<(FlowInfo Caller, Step Step)>();
                    references[target.Id ?? step.MacroFlowId!] = callers;
                }
                callers.Add((flow, step));
            }
        }

        foreach (var pair in references)
        {
            if (pair.Value.Count > 1 && flowById.TryGetValue(pair.Key, out var target))
            {
                throw new InvalidOperationException($"MacroStep {target.Name ?? target.Id} is referenced by multiple macro steps.");
            }
        }
    }
    private static IList<MacroBindingContext> BuildMacroBindings(IList<FlowInfo> flows)
    {
        var flowById = flows
            .Where(flow => !string.IsNullOrWhiteSpace(flow.Id))
            .ToDictionary(flow => flow.Id!, StringComparer.OrdinalIgnoreCase);

        return flows
            .Where(flow => string.Equals(NormalizeDiagramType(flow), "Macro", StringComparison.OrdinalIgnoreCase))
            .SelectMany(flow => (flow.Steps ?? new List<Step>())
                .Where(step => string.Equals(step.Kind, "macro", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(step.MacroFlowId)
                    && flowById.TryGetValue(step.MacroFlowId!, out var callee)
                    && string.Equals(NormalizeDiagramType(callee), "MacroStep", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(ResolveFlowUnitId(flow), ResolveFlowUnitId(callee), StringComparison.OrdinalIgnoreCase))
                .Select(step =>
                {
                    var callee = flowById[step.MacroFlowId!];
                    return new MacroBindingContext
                    {
                        unitId = ResolveFlowUnitId(flow),
                        callerFlowId = flow.Id ?? string.Empty,
                        callerStepId = step.Id,
                        calleeFlowId = callee.Id ?? string.Empty,
                        portName = BuildMacroPortName(callee),
                        variable = ResolveMacroPortVariable(callee)
                    };
                }))
            .ToList();
    }


    private static DeviceVariable? ResolveMacroPortVariable(FlowInfo flow)
        => flow.MacroPortVariable;

    private static void ValidateMacroPortVariables(IList<FlowInfo> flows, IList<DeviceVariable> variables)
    {
        foreach (var flow in flows.Where(flow => string.Equals(NormalizeDiagramType(flow), "MacroStep", StringComparison.OrdinalIgnoreCase)))
        {
            var name = flow.Name ?? flow.Id ?? string.Empty;
            var matches = variables
                .Where(variable => !string.IsNullOrWhiteSpace(variable.Label)
                    && string.Equals(variable.Label, name, StringComparison.OrdinalIgnoreCase))
                .ToList();

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
    }

    private static string BuildMacroPortName(FlowInfo flow)
    {
        var source = !string.IsNullOrWhiteSpace(flow.Name) ? flow.Name! : flow.Id ?? "MacroStep";
        var token = new string(source.Trim().Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray()).Trim('_');
        return flow.MacroPortVariable?.Label ?? $"{(string.IsNullOrWhiteSpace(token) ? "MacroStep" : token)}_Port";
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



internal static class StepLabelExtensions
{
    public static string LabelOrId(this Step step) => !string.IsNullOrWhiteSpace(step.Label) ? step.Label : step.Id;
    private readonly record struct ParsedBoolBase(string Prefix, int Number, int Width);
}

