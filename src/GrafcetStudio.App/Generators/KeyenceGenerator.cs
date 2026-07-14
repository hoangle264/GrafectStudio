using GrafcetStudio.App.Generators.Keyence;
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

    private static readonly string[] ExpressionSectionTemplateOrder =
    [
        "uc.main",
        "uc.flows"
    ];

    private static readonly (string TemplateId, string PartialName)[] KnownPartials =
    [
        ("uc.flow", "flow"),
        ("uc.step", "step"),
        ("uc.stepExpression", "step_expression"),
        ("uc.actionExpression", "action_expression"),
        ("uc.outputExpression", "output_expression"),
        ("uc.transitionExpression", "transition_expression"),
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

    public override string Platform => "Keyence";
    public string GenerateUnitContent(CodegenPayload payload) => GenerateLegacy(payload);
    protected override string GenerateLegacy(CodegenPayload payload)
    {
         var context = BuildContext(payload);
        RegisterPartials();

        var renderedSections = ResolveSectionTemplateNames()
            .Select(templateName => _templates.TryRender(templateName, context, out var result) ? result : string.Empty)
            .Where(section => !string.IsNullOrWhiteSpace(section))
            .ToList();

        if (renderedSections.Count == 0)
        {
            return JsonSerializer.Serialize(context, JsonOptions);
        }

        var rendered = string.Join(Environment.NewLine, renderedSections);
        return RenderedOutputLooksExpressionBased(rendered)
            ? ConvertRenderedPseudoExpressionToMnemonic(rendered, payload.Variables)
            : rendered;
    }


    private static bool RenderedOutputLooksExpressionBased(string rendered)
        => !string.IsNullOrWhiteSpace(rendered)
            && (rendered.Contains("->", StringComparison.Ordinal)
                || rendered.Contains("-&gt;", StringComparison.OrdinalIgnoreCase));

    private static string ConvertRenderedPseudoExpressionToMnemonic(string rendered, IList<DeviceVariable> vars)
    {
        if (string.IsNullOrWhiteSpace(rendered)) return rendered;

        var lines = rendered.Replace("\r", string.Empty).Split('\n');
        var output = new List<string>(lines.Length);

        foreach (var rawLine in lines)
        {
            if (TryConvertPseudoExpressionLine(rawLine, vars, out var mnemonicLines))
            {
                output.AddRange(mnemonicLines);
                continue;
            }

            output.Add(rawLine);
        }

        return string.Join(Environment.NewLine, output);
    }

    private static bool TryConvertPseudoExpressionLine(string? rawLine, IList<DeviceVariable> vars, out IList<string> mnemonicLines)
    {
        mnemonicLines = new List<string>();
        if (string.IsNullOrWhiteSpace(rawLine)) return false;

        var trimmed = System.Net.WebUtility.HtmlDecode(rawLine).Trim();
        if (trimmed.StartsWith(";", StringComparison.Ordinal) || !trimmed.Contains("->", StringComparison.Ordinal)) return false;

        var arrowIndex = trimmed.IndexOf("->", StringComparison.Ordinal);
        if (arrowIndex < 0 || arrowIndex >= trimmed.Length - 2) return false;

        var condition = trimmed[..arrowIndex].Trim();
        var instructionPart = trimmed[(arrowIndex + 2)..].Trim();
        var instructionSplit = instructionPart.Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries);
        if (instructionSplit.Length < 2) return false;

        var instruction = instructionSplit[0].Trim();
        var target = instructionSplit[1].Trim();
        if (string.IsNullOrWhiteSpace(instruction)
            || string.IsNullOrWhiteSpace(target)
            || target.Contains("->", StringComparison.Ordinal))
        {
            return false;
        }

        mnemonicLines = MnemonicEmitter.EmitRungLines(condition, instruction, target, vars);
        return mnemonicLines.Count > 0;
    }
    private IEnumerable<string> ResolveSectionTemplateNames()
    {
        if (_templates.IsTemplateLoaded("uc.main"))
        {
            yield return "uc.main";
            yield break;
        }

        if (_templates.IsTemplateLoaded("uc.flows"))
        {
            yield return "uc.flows";
        }
        else
        {
            foreach (var templateName in SectionTemplateOrder)
            {
                if (_templates.IsTemplateLoaded(templateName)) yield return templateName;
            }
        }

        if (_templates.IsTemplateLoaded("uc.outputs"))
        {
            yield return "uc.outputs";
        }
        else if (_templates.IsTemplateLoaded("uc.mainOutput"))
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
        foreach (var templateName in ExpressionSectionTemplateOrder)
        {
            RegisterPartialIfLoaded(templateName, templateName[3..]);
        }

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
        var macroBindings = AnalyzeMacroFlows(flows, payload.Variables);
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
        var resolvedFlowResults = flows.Select(flow => BuildResolvedFlow(flow, payload.Variables, library, macroBindings)).ToList();
        var resolvedFlows = resolvedFlowResults.Select(result => result.Flow).ToList();
        var runtimePlans = resolvedFlowResults.Select(result => result.RuntimePlan).ToList();
        var outputBindings = MergeOutputBindings(runtimePlans.SelectMany(plan => plan.OutputBindingPlan.Bindings));
        var unitVariable = FindUnitVariable(payload.Variables, unitLabel);
        var unitAddresses = unitVariable?.SignalAddresses ?? new Dictionary<string, string>();
        var deviceOutputGroups = BuildDeviceOutputGroups(outputBindings, payload.Variables, payload.DeviceTypes, unitAddresses);
        var unitStepRange = StepAddressHelper.BuildUnitStepAddressRange(resolvedFlows);
        var macroFlows = resolvedFlows.Where(f => string.Equals(f.diagramType, "Macro", StringComparison.OrdinalIgnoreCase)).ToList();
        var macroStepFlows = resolvedFlows.Where(f => string.Equals(f.diagramType, "MacroStep", StringComparison.OrdinalIgnoreCase)).ToList();
        var autoFlows = macroFlows.Where(f => string.Equals(f.normalizedType, "auto", StringComparison.OrdinalIgnoreCase)).ToList();
        var originFlows = macroFlows.Where(f => string.Equals(f.normalizedType, "origin", StringComparison.OrdinalIgnoreCase)).ToList();
        var flowGroups = new[]
        {
            BuildFlowGroup("auto", autoFlows),
            BuildFlowGroup("origin", originFlows),
            BuildFlowGroup("macro", macroFlows),
            BuildFlowGroup("macroStep", macroStepFlows)
        };
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
            flows = resolvedFlows,
            flowGroups,
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




    private ResolvedFlowBuildResult BuildResolvedFlow(FlowInfo flow, IList<DeviceVariable> variables, DeviceLibraryRoot library, IList<MacroBindingContext> macroBindings)
    {
        var state = flow.ToDiagramState(variables);
        var sequence = _sequenceResolver.Resolve(state);
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
                BranchType = entry.BranchType,
                Expression = StepContextBuilder.BuildStepExpressionContext(step, previousStep, nextStep, entry.InTransition, entry.OutTransition, index == 0, variables)
            };
        }).ToList();
        var flowStepRange = StepAddressHelper.BuildFlowStepAddressRange(flow);
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
    private static object BuildFlowGroup(string key, IList<ResolvedFlow> flows)
        => new
        {
            key,
            name = key,
            flows,
            count = flows.Count,
            hasFlows = flows.Count > 0,
            isEmpty = flows.Count == 0
        };

    private sealed record ResolvedFlowBuildResult(ResolvedFlow Flow, DiagramRuntimePlan RuntimePlan);


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
                var deviceKind = NormalizeDeviceKind(firstSource.DeviceFormat);

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
                    .OrderBy(commandGroup => commandGroup.Key, StringComparer.OrdinalIgnoreCase)
                    .Select((commandGroup, commandIndex) =>
                    {
                        var commandSource = commandGroup.Select(item => item.Source).First();
                        var commandBindings = commandGroup.Select(item => item.Binding).ToList();
                        var aggregationMode = commandGroup
                            .Select(item => item.Binding.AggregationMode)
                            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "OR";
                        var physicalOutputRef = commandGroup
                            .Select(item => item.Binding.PhysicalOutputRef)
                            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
                        var interlockSignal = commandGroup
                            .Select(item => item.Source.InterlockSignal)
                            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
                        var interlockAddress = commandGroup
                            .Select(item => item.Source.InterlockAddress)
                            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
                        var interlockLabel = commandGroup
                            .Select(item => item.Source.InterlockLabel)
                            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
                        var interlockRequiredState = commandGroup
                            .Select(item => item.Source.InterlockRequiredState)
                            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
                        var flowCommands = BuildCommandFlowOutputs(commandGroup.Select(item => item.Source), unitAddresses);
                        var originCommandCount = flowCommands.Count(command => command.IsOrigin);
                        var autoCommandCount = flowCommands.Count(command => command.IsAuto);
                        var sourceConditionExpression = ExpressionHelper.JoinByAggregationMode(flowCommands.Select(command => command.conditionExpression), aggregationMode);
                        var autoConditionExpression = ExpressionHelper.JoinByAggregationMode(flowCommands.Where(command => command.IsAuto).Select(command => command.conditionExpression), aggregationMode);
                        var originConditionExpression = ExpressionHelper.JoinByAggregationMode(flowCommands.Where(command => command.IsOrigin).Select(command => command.conditionExpression), aggregationMode);
                        var manualConditionExpression = ResolveModeFlagAddress("manual", unitAddresses);
                        var interlockExpression = BuildInterlockExpression(interlockAddress, interlockRequiredState);
                        var gatedSourceConditionExpression = !string.IsNullOrWhiteSpace(interlockExpression)
                            ? ExpressionHelper.WrapConditionTerm(sourceConditionExpression)
                            : sourceConditionExpression;
                        var driveConditionExpression = ExpressionHelper.JoinAnd(new[] { gatedSourceConditionExpression, interlockExpression });
                        var instruction = ResolveOutputInstruction(commandGroup.Select(item => item.Source.Qualifier));
                        var expression = StepContextBuilder.BuildInstructionExpression(driveConditionExpression, instruction, physicalOutputRef);
                        var feedbackSignals = commandGroup
                            .SelectMany(item => item.Source.FeedbackSignals)
                            .GroupBy(signal => $"{signal.SignalName}\u001F{signal.PhysicalAddress}", StringComparer.OrdinalIgnoreCase)
                            .Select(signalGroup => signalGroup.First())
                            .ToList();
                        var mnemonicLines = MnemonicEmitter.EmitRungLines(driveConditionExpression, instruction, physicalOutputRef, variables);
                        var mnemonic = MnemonicEmitter.JoinMnemonicLines(mnemonicLines);
                        var outputIntent = new DeviceOutputIntent
                        {
                            index = commandIndex,
                            number = commandIndex + 1,
                            deviceLabel = deviceGroup.Key,
                            deviceFormat = firstSource.DeviceFormat,
                            deviceKind = deviceKind,
                            commandId = commandSource.CommandId,
                            actionLabel = commandSource.ActionLabel,
                            driveSignal = commandSource.DriveSignal,
                            conditionExpression = driveConditionExpression,
                            sourceConditionExpression = sourceConditionExpression,
                            autoConditionExpression = autoConditionExpression,
                            originConditionExpression = originConditionExpression,
                            manualConditionExpression = manualConditionExpression,
                            interlockExpression = interlockExpression,
                            driveConditionExpression = driveConditionExpression,
                            instruction = instruction,
                            target = physicalOutputRef,
                            expression = expression,
                            mnemonic = mnemonic,
                            mnemonicLines = mnemonicLines,
                            sources = flowCommands,
                            feedbackSignals = feedbackSignals
                        };

                        return new DeviceCommandOutput
                        {
                            CommandId = commandSource.CommandId,
                            ActionLabel = commandSource.ActionLabel,
                            DriveSignal = commandSource.DriveSignal,
                            InterlockSignal = interlockSignal,
                            InterlockAddress = interlockAddress,
                            InterlockLabel = interlockLabel,
                            InterlockRequiredState = interlockRequiredState,
                            HasInterlock = !string.IsNullOrWhiteSpace(interlockExpression),
                            PhysicalOutputRef = physicalOutputRef,
                            AggregationMode = aggregationMode,
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
                            FeedbackSignals = feedbackSignals,
                            output = outputIntent,
                            sources = flowCommands,
                            conditionExpression = driveConditionExpression,
                            sourceConditionExpression = sourceConditionExpression,
                            autoConditionExpression = autoConditionExpression,
                            originConditionExpression = originConditionExpression,
                            manualConditionExpression = manualConditionExpression,
                            interlockExpression = interlockExpression,
                            driveConditionExpression = driveConditionExpression,
                            instruction = instruction,
                            target = physicalOutputRef,
                            expression = expression,
                            mnemonic = mnemonic,
                            mnemonicLines = mnemonicLines
                        };
                    })
                    .ToList();
                var outputs = commands.Select(command => command.output).ToList();

                return new DeviceOutputGroup
                {
                    DeviceLabel = deviceGroup.Key,
                    DeviceFormat = firstSource.DeviceFormat,
                    DeviceKind = deviceKind,
                    Address = variable?.Address,
                    SignalAddresses = variable?.SignalAddresses ?? new Dictionary<string, string>(),
                    UnitAddresses = new Dictionary<string, string>(unitAddresses, StringComparer.OrdinalIgnoreCase),
                    Signals = signals,
                    Commands = commands,
                    outputs = outputs
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


    private static IList<DeviceCommandFlowOutput> BuildCommandFlowOutputs(
        IEnumerable<OutputBindingSource> sources,
        IDictionary<string, string> unitAddresses)
    {
        var commands = sources
            .Where(source => !string.IsNullOrWhiteSpace(source.SourceExecuteBitRef) || !string.IsNullOrWhiteSpace(source.SourceDoneBitRef))
            .GroupBy(source => $"{source.FlowType}\u001F{source.FlowId}\u001F{source.FlowName}\u001F{source.CommandId}\u001F{source.ActionLabel}\u001F{source.SourceStep}\u001F{source.SourceExecuteBitRef}\u001F{source.SourceDoneBitRef}\u001F{source.ActionSymbol}\u001F{source.Qualifier}", StringComparer.OrdinalIgnoreCase)
            .Select(commandGroup =>
            {
                var source = commandGroup.First();
                var flowType = string.Equals(source.FlowType, "origin", StringComparison.OrdinalIgnoreCase) ? "origin" : "auto";
                var modeFlagAddress = ResolveModeFlagAddress(flowType, unitAddresses);
                var executeExpression = source.SourceExecuteBitRef ?? string.Empty;
                var doneGuardExpression = ExpressionHelper.NegateExpression(source.SourceDoneBitRef);
                var conditionExpression = ExpressionHelper.JoinAnd(new[] { modeFlagAddress, executeExpression, doneGuardExpression });

                return new DeviceCommandFlowOutput
                {
                    Id = source.FlowId,
                    Name = source.FlowName,
                    FlowType = flowType,
                    IsOrigin = string.Equals(flowType, "origin", StringComparison.OrdinalIgnoreCase),
                    IsAuto = string.Equals(flowType, "auto", StringComparison.OrdinalIgnoreCase),
                    CommandId = source.CommandId ?? string.Empty,
                    ActionLabel = source.ActionLabel ?? string.Empty,
                    SourceStep = source.SourceStep ?? string.Empty,
                    SourceExecuteBit = source.SourceExecuteBitRef ?? string.Empty,
                    SourceDoneBit = source.SourceDoneBitRef ?? string.Empty,
                    actionSymbol = source.ActionSymbol ?? string.Empty,
                    qualifier = source.Qualifier ?? string.Empty,
                    modeFlagAddress = modeFlagAddress,
                    executeExpression = executeExpression,
                    doneGuardExpression = doneGuardExpression,
                    conditionExpression = conditionExpression,
                    conditionMnemonic = string.Empty,
                    conditionMnemonicLines = new List<string>()
                };
            })
            .OrderBy(command => command.IsAuto)
            .ThenBy(command => command.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(command => command.Id, StringComparer.OrdinalIgnoreCase)
            .ThenBy(command => command.CommandId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(command => command.SourceExecuteBit, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return commands
            .Select((command, index) =>
            {
                var conditionMnemonicLines = MnemonicEmitter.EmitConditionLines(command.conditionExpression, Array.Empty<DeviceVariable>());
                return new DeviceCommandFlowOutput
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
                    actionSymbol = command.actionSymbol,
                    qualifier = command.qualifier,
                    modeFlagAddress = command.modeFlagAddress,
                    executeExpression = command.executeExpression,
                    doneGuardExpression = command.doneGuardExpression,
                    conditionExpression = command.conditionExpression,
                    conditionMnemonic = MnemonicEmitter.JoinMnemonicLines(conditionMnemonicLines),
                    conditionMnemonicLines = conditionMnemonicLines,
                    Index = index,
                    Number = index + 1,
                    TotalCount = commands.Count,
                    IsFirst = index == 0,
                    IsLast = index == commands.Count - 1,
                    IsSingle = commands.Count == 1
                };
            })
            .ToList();
    }
    private static string BuildInterlockExpression(string? interlockAddress, string? requiredState)
    {
        if (string.IsNullOrWhiteSpace(interlockAddress)) return string.Empty;

        return ExpressionHelper.IsFalseState((requiredState ?? string.Empty).Trim())
            ? ExpressionHelper.NegateExpression(interlockAddress)
            : interlockAddress.Trim();
    }

    private static string ResolveOutputInstruction(IEnumerable<string?> qualifiers)
    {
        var first = qualifiers.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        return string.IsNullOrWhiteSpace(first) ? "OUT" : StepContextBuilder.ResolveActionInstruction(first!);
    }

    private static string ResolveModeFlagAddress(string flowType, IDictionary<string, string> unitAddresses)
    {
        var key = string.Equals(flowType, "origin", StringComparison.OrdinalIgnoreCase)
            ? "flagOrigin"
            : string.Equals(flowType, "manual", StringComparison.OrdinalIgnoreCase)
                ? "flagManual"
                : "flagAuto";

        return TryGetAddress(unitAddresses, key, out var address) ? address : string.Empty;
    }

    private static bool TryGetAddress(IDictionary<string, string> addresses, string key, out string address)
    {
        if (addresses.TryGetValue(key, out address!) && !string.IsNullOrWhiteSpace(address))
        {
            address = address.Trim();
            return true;
        }

        var match = addresses.FirstOrDefault(pair => string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase));
        address = match.Value ?? string.Empty;
        if (string.IsNullOrWhiteSpace(address)) return false;

        address = address.Trim();
        return true;
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



