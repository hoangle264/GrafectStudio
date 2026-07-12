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

        return renderedSections.Count == 0
            ? JsonSerializer.Serialize(context, JsonOptions)
            : string.Join(Environment.NewLine, renderedSections);
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
        var flowGroups = new[]
        {
            BuildFlowGroup("auto", autoFlows),
            BuildFlowGroup("origin", originFlows),
            BuildFlowGroup("macro", macroFlows),
            BuildFlowGroup("macroStep", macroStepFlows)
        };
        var templateContract = new
        {
            name = "keyence-step-expression",
            version = 1,
            flowBased = true,
            stepBody = "expression",
            stepExpressionPath = "flow.steps[].expression"
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
            templateContract,
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




    private ResolvedFlow BuildResolvedFlow(FlowInfo flow, IList<DeviceVariable> variables, DeviceLibraryRoot library, IList<MacroBindingContext> macroBindings)
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
                Expression = BuildStepExpressionContext(step, previousStep, nextStep, entry.InTransition, entry.OutTransition, index == 0, variables, library)
            };
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

    private static StepExpressionContext BuildStepExpressionContext(
        Step step,
        Step? previousStep,
        Step? nextStep,
        Transition? inTransition,
        Transition? outTransition,
        bool isFirstStep,
        IList<DeviceVariable> variables,
        DeviceLibraryRoot library)
    {
        var inTransitionExpression = BuildConditionExpression(inTransition?.Condition);
        var outTransitionExpression = BuildConditionExpression(outTransition?.Condition);
        var activationExpression = BuildActivationExpression(step, previousStep, inTransitionExpression, isFirstStep);
        var holdExpression = BuildHoldExpression(step, outTransitionExpression);
        var actions = BuildStepActionExpressions(step, variables);
        var outputs = BuildStepOutputExpressions(step, variables, library);
        var doneConditionExpression = BuildDoneConditionExpression(step, outTransitionExpression, actions);
        var doneInstruction = string.IsNullOrWhiteSpace(step.DoneAddress) ? string.Empty : "SET";
        var doneTarget = step.DoneAddress ?? string.Empty;
        var activationMnemonicLines = EmitConditionLines(activationExpression, variables);
        var activationRungLines = EmitRungLines(activationExpression, "SET", step.ExecAddress ?? string.Empty, variables);
        var holdMnemonicLines = EmitConditionLines(holdExpression, variables);
        var doneConditionMnemonicLines = EmitConditionLines(doneConditionExpression, variables);
        var doneMnemonicLines = EmitRungLines(doneConditionExpression, doneInstruction, doneTarget, variables);
        var outputExpression = string.Join(" ; ", outputs.Select(output => output.expression).Where(value => !string.IsNullOrWhiteSpace(value)));
        var outputMnemonicBlocks = outputs.Select(output => output.mnemonic).Where(value => !string.IsNullOrWhiteSpace(value)).ToList();
        var outputMnemonicLines = outputs.SelectMany(output => output.mnemonicLines).ToList();
        var bodyExpressions = BuildBodyExpressions(
            BuildInstructionExpression(activationExpression, "SET", step.ExecAddress ?? string.Empty),
            outputExpression,
            BuildInstructionExpression(doneConditionExpression, doneInstruction, doneTarget));
        var bodyMnemonics = BuildBodyMnemonics(activationRungLines, outputs, doneMnemonicLines);

        return new StepExpressionContext
        {
            conditionExpression = activationExpression,
            activationExpression = activationExpression,
            holdExpression = holdExpression,
            inTransitionExpression = inTransitionExpression,
            outTransitionExpression = outTransitionExpression,
            doneConditionExpression = doneConditionExpression,
            doneInstruction = doneInstruction,
            doneTarget = doneTarget,
            doneExpression = BuildInstructionExpression(doneConditionExpression, doneInstruction, doneTarget),
            bodyExpression = string.Join(" ; ", bodyExpressions),
            outputInstruction = outputs.FirstOrDefault()?.instruction ?? string.Empty,
            outputTarget = outputs.FirstOrDefault()?.target ?? string.Empty,
            outputExpression = outputExpression,
            conditionMnemonic = JoinMnemonicLines(activationMnemonicLines),
            conditionMnemonicLines = activationMnemonicLines,
            activationMnemonic = JoinMnemonicLines(activationRungLines),
            activationMnemonicLines = activationRungLines,
            holdMnemonic = JoinMnemonicLines(holdMnemonicLines),
            holdMnemonicLines = holdMnemonicLines,
            doneMnemonic = JoinMnemonicLines(doneMnemonicLines),
            doneMnemonicLines = doneMnemonicLines,
            doneConditionMnemonicLines = doneConditionMnemonicLines,
            outputMnemonic = JoinMnemonicBlocks(outputMnemonicBlocks),
            outputMnemonicLines = outputMnemonicLines,
            bodyMnemonic = JoinMnemonicBlocks(bodyMnemonics),
            bodyMnemonicLines = bodyMnemonics.SelectMany(block => SplitMnemonicLines(block)).ToList(),
            bodyExpressions = bodyExpressions,
            bodyMnemonics = bodyMnemonics,
            actions = actions,
            outputs = outputs,
            inTransition = BuildTransitionExpression(inTransition, inTransitionExpression),
            outTransition = BuildTransitionExpression(outTransition, outTransitionExpression)
        };
    }

    private static string BuildActivationExpression(Step step, Step? previousStep, string inTransitionExpression, bool isFirstStep)
    {
        var terms = new List<string>();
        if (!isFirstStep && !step.IsInitial && !string.IsNullOrWhiteSpace(previousStep?.DoneAddress))
        {
            terms.Add(previousStep!.DoneAddress!);
        }

        AddConditionTerm(terms, inTransitionExpression);
        return JoinAnd(terms);
    }

    private static string BuildHoldExpression(Step step, string outTransitionExpression)
    {
        var terms = new List<string>();
        AddConditionTerm(terms, step.ExecAddress);
        AddConditionTerm(terms, NegateExpression(outTransitionExpression));
        return JoinAnd(terms);
    }

    private static string BuildDoneConditionExpression(Step step, string outTransitionExpression, IList<StepActionExpressionContext> actions)
    {
        var terms = new List<string>();
        AddConditionTerm(terms, step.ExecAddress);
        foreach (var action in actions)
        {
            AddConditionTerm(terms, action.completionExpression);
        }
        AddConditionTerm(terms, outTransitionExpression);
        return JoinAnd(terms);
    }

    private static IList<StepActionExpressionContext> BuildStepActionExpressions(Step step, IList<DeviceVariable> variables)
        => step.Actions
            .Select((action, index) =>
            {
                var target = ResolveActionTarget(action, variables);
                var instruction = ResolveActionInstruction(action.Qualifier.ToString());
                var conditionExpression = step.ExecAddress ?? string.Empty;
                var completion = BuildCompletionExpression(action.Complete);
                var completionExpression = completion?.address ?? string.Empty;

                return new StepActionExpressionContext
                {
                    index = index,
                    number = index + 1,
                    variable = action.Variable,
                    address = action.Address ?? string.Empty,
                    qualifier = action.Qualifier.ToString(),
                    timeMs = action.TimeMs,
                    conditionExpression = conditionExpression,
                    instruction = instruction,
                    target = target,
                    expression = BuildInstructionExpression(conditionExpression, instruction, target),
                    mnemonic = EmitRung(conditionExpression, instruction, target, variables),
                    mnemonicLines = EmitRungLines(conditionExpression, instruction, target, variables),
                    completionExpression = completionExpression,
                    completion = completion
                };
            })
            .ToList();

    private static IList<StepOutputExpressionContext> BuildStepOutputExpressions(
        Step step,
        IList<DeviceVariable> variables,
        DeviceLibraryRoot library)
    {
        var outputs = new List<StepOutputExpressionContext>();
        for (var actionIndex = 0; actionIndex < step.Actions.Count; actionIndex++)
        {
            var action = step.Actions[actionIndex];
            var resolved = DeviceCommandResolver.Resolve(action, step.ExecAddress ?? string.Empty, variables, library);
            if (resolved is null || resolved.OutputBindings.Count == 0)
            {
                AddDirectActionOutput(outputs, action, actionIndex, step, variables);
                continue;
            }

            foreach (var binding in resolved.OutputBindings)
            {
                if (string.IsNullOrWhiteSpace(binding.PhysicalOutputRef)) continue;

                var interlockExpression = BuildInterlockExpression(binding);
                var conditionExpression = JoinAnd(new[] { step.ExecAddress ?? string.Empty, interlockExpression });
                var expression = BuildInstructionExpression(conditionExpression, "OUT", binding.PhysicalOutputRef);
                outputs.Add(new StepOutputExpressionContext
                {
                    index = outputs.Count,
                    number = outputs.Count + 1,
                    conditionExpression = conditionExpression,
                    instruction = "OUT",
                    target = binding.PhysicalOutputRef,
                    expression = expression,
                    mnemonic = EmitRung(conditionExpression, "OUT", binding.PhysicalOutputRef, variables),
                    mnemonicLines = EmitRungLines(conditionExpression, "OUT", binding.PhysicalOutputRef, variables),
                    deviceLabel = binding.DeviceLabel,
                    deviceFormat = binding.DeviceFormat,
                    commandId = binding.CommandId,
                    actionLabel = binding.ActionLabel,
                    driveSignal = binding.DriveSignal,
                    interlockExpression = interlockExpression,
                    feedbackSignals = binding.FeedbackSignals
                        .Select(signal => new StepFeedbackExpressionContext
                        {
                            signalName = signal.SignalName,
                            label = signal.Label,
                            address = signal.PhysicalAddress
                        })
                        .ToList()
                });
            }
        }

        return outputs;
    }

    private static void AddDirectActionOutput(
        IList<StepOutputExpressionContext> outputs,
        StepAction action,
        int actionIndex,
        Step step,
        IList<DeviceVariable> variables)
    {
        var target = !string.IsNullOrWhiteSpace(action.Address)
            ? action.Address!
            : SignalResolver.ResolveAddress(action.Variable, variables) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(target)) return;

        var conditionExpression = step.ExecAddress ?? string.Empty;
        outputs.Add(new StepOutputExpressionContext
        {
            index = outputs.Count,
            number = outputs.Count + 1,
            conditionExpression = conditionExpression,
            instruction = ResolveActionInstruction(action.Qualifier.ToString()),
            target = target,
            expression = BuildInstructionExpression(conditionExpression, ResolveActionInstruction(action.Qualifier.ToString()), target),
            mnemonic = EmitRung(conditionExpression, ResolveActionInstruction(action.Qualifier.ToString()), target, variables),
            mnemonicLines = EmitRungLines(conditionExpression, ResolveActionInstruction(action.Qualifier.ToString()), target, variables),
            commandId = action.Variable,
            actionLabel = action.Variable
        });
    }

    private static StepTransitionExpressionContext? BuildTransitionExpression(Transition? transition, string expression)
        => transition is null
            ? null
            : new StepTransitionExpressionContext
            {
                id = transition.Id,
                label = transition.Label,
                condition = transition.Condition,
                expression = expression
            };

    private static StepCompletionExpressionContext? BuildCompletionExpression(StepActionCompletion? completion)
        => completion is null
            ? null
            : new StepCompletionExpressionContext
            {
                sensor = completion.Sensor,
                sensorLabel = completion.SensorLabel,
                address = completion.Address
            };

    private static string ResolveActionTarget(StepAction action, IList<DeviceVariable> variables)
    {
        if (!string.IsNullOrWhiteSpace(action.Address)) return action.Address!;

        var resolved = SignalResolver.ResolveAddress(action.Variable, variables);
        return string.IsNullOrWhiteSpace(resolved) ? action.Variable : resolved!;
    }

    private static string ResolveActionInstruction(string qualifier)
        => qualifier.ToUpperInvariant() switch
        {
            "S" or "SD" or "SL" => "SET",
            "R" => "RST",
            _ => "OUT"
        };

    private static string BuildConditionExpression(string? condition)
    {
        var value = (condition ?? string.Empty).Trim();
        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ? string.Empty : value;
    }

    private static string BuildInterlockExpression(OutputBinding binding)
    {
        if (string.IsNullOrWhiteSpace(binding.InterlockAddress)) return string.Empty;

        var requiredState = (binding.InterlockRequiredState ?? string.Empty).Trim();
        return IsFalseState(requiredState) ? NegateExpression(binding.InterlockAddress) : binding.InterlockAddress;
    }

    private static bool IsFalseState(string value)
        => string.Equals(value, "0", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "false", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "off", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "low", StringComparison.OrdinalIgnoreCase);

    private static IList<string> BuildBodyExpressions(params string[] expressions)
        => expressions
            .Where(expression => !string.IsNullOrWhiteSpace(expression))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static string BuildInstructionExpression(string conditionExpression, string instruction, string target)
    {
        if (string.IsNullOrWhiteSpace(instruction) || string.IsNullOrWhiteSpace(target)) return string.Empty;

        return string.IsNullOrWhiteSpace(conditionExpression)
            ? $"{instruction} {target}"
            : $"{conditionExpression} -> {instruction} {target}";
    }

    private static string EmitRung(string condition, string instruction, string target, IList<DeviceVariable> vars)
        => JoinMnemonicLines(EmitRungLines(condition, instruction, target, vars));

    private static IList<string> EmitRungLines(string condition, string instruction, string target, IList<DeviceVariable> vars)
    {
        if (string.IsNullOrWhiteSpace(instruction) || string.IsNullOrWhiteSpace(target)) return new List<string>();

        var keyenceInstruction = ToInstruction(instruction, target);
        if (keyenceInstruction is null) return new List<string>();

        if (string.IsNullOrWhiteSpace(condition))
        {
            return SplitMnemonicLines(KeyenceMnemonicInstructionEmitter.Format(keyenceInstruction));
        }

        try
        {
            return SplitMnemonicLines(KeyenceMnemonicExpressionEmitter.EmitExpressionAndInstruction(condition, vars.ToList(), keyenceInstruction));
        }
        catch
        {
            return SplitMnemonicLines(BuildInstructionExpression(condition, instruction, target));
        }
    }

    private static IList<string> EmitConditionLines(string condition, IList<DeviceVariable> vars)
    {
        if (string.IsNullOrWhiteSpace(condition)) return new List<string>();

        try
        {
            return KeyenceMnemonicExpressionEmitter.EmitCondition(condition, vars.ToList()).ToList();
        }
        catch
        {
            return new List<string> { condition };
        }
    }

    private static KeyenceInstruction? ToInstruction(string instruction, string target)
    {
        if (string.IsNullOrWhiteSpace(instruction) || string.IsNullOrWhiteSpace(target)) return null;

        return instruction.Trim().ToUpperInvariant() switch
        {
            "OUT" => KeyenceOutputInstruction.Create(KeyenceInstructionType.Out, target),
            "SET" => KeyenceOutputInstruction.Create(KeyenceInstructionType.Set, target),
            "RST" => KeyenceOutputInstruction.Create(KeyenceInstructionType.Rst, target),
            "RES" => KeyenceOutputInstruction.Create(KeyenceInstructionType.Res, target),
            _ => null
        };
    }

    private static string JoinMnemonicLines(IEnumerable<string> lines)
        => string.Join(Environment.NewLine, lines.Where(line => !string.IsNullOrWhiteSpace(line)));

    private static string JoinMnemonicBlocks(IEnumerable<string> blocks)
        => string.Join(Environment.NewLine, blocks.Where(block => !string.IsNullOrWhiteSpace(block)));

    private static IList<string> SplitMnemonicLines(string text)
        => string.IsNullOrWhiteSpace(text)
            ? new List<string>()
            : text.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();

    private static IList<string> BuildBodyMnemonics(
        IList<string> activationRungLines,
        IList<StepOutputExpressionContext> outputs,
        IList<string> doneMnemonicLines)
    {
        var blocks = new List<string>();
        var activationBlock = JoinMnemonicLines(activationRungLines);
        if (!string.IsNullOrWhiteSpace(activationBlock)) blocks.Add(activationBlock);

        foreach (var output in outputs)
        {
            if (!string.IsNullOrWhiteSpace(output.mnemonic)) blocks.Add(output.mnemonic);
        }

        var doneBlock = JoinMnemonicLines(doneMnemonicLines);
        if (!string.IsNullOrWhiteSpace(doneBlock)) blocks.Add(doneBlock);
        return blocks
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AddConditionTerm(IList<string> terms, string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || string.Equals(value.Trim(), "1", StringComparison.OrdinalIgnoreCase)) return;
        terms.Add(value.Trim());
    }

    private static string JoinAnd(IEnumerable<string?> terms)
        => string.Join(" & ", terms
            .Where(term => !string.IsNullOrWhiteSpace(term) && !string.Equals(term.Trim(), "1", StringComparison.OrdinalIgnoreCase))
            .Select(term => term!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase));

    private static string NegateExpression(string? expression)
    {
        if (string.IsNullOrWhiteSpace(expression)) return string.Empty;

        var value = expression.Trim();
        if (value.StartsWith("!", StringComparison.Ordinal))
        {
            return value[1..];
        }

        return IsSimpleOperand(value) ? $"!{value}" : $"!({value})";
    }

    private static bool IsSimpleOperand(string expression)
    {
        var value = expression.Trim();
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (value.Contains('&', StringComparison.Ordinal) || value.Contains('|', StringComparison.Ordinal) || value.Contains('(', StringComparison.Ordinal) || value.Contains(')', StringComparison.Ordinal) || value.Contains(' ', StringComparison.Ordinal))
        {
            return false;
        }

        return true;
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
            if (TryParseAddressBase(diagram?.BaseMr, out var baseMr) && string.Equals(diagram?.BoolAddressMode, "block", StringComparison.OrdinalIgnoreCase))
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
                        var sourceConditionExpression = JoinByAggregationMode(flowCommands.Select(command => command.conditionExpression), aggregationMode);
                        var autoConditionExpression = JoinByAggregationMode(flowCommands.Where(command => command.IsAuto).Select(command => command.conditionExpression), aggregationMode);
                        var originConditionExpression = JoinByAggregationMode(flowCommands.Where(command => command.IsOrigin).Select(command => command.conditionExpression), aggregationMode);
                        var manualConditionExpression = ResolveModeFlagAddress("manual", unitAddresses);
                        var interlockExpression = BuildInterlockExpression(interlockAddress, interlockRequiredState);
                        var gatedSourceConditionExpression = !string.IsNullOrWhiteSpace(interlockExpression)
                            ? WrapConditionTerm(sourceConditionExpression)
                            : sourceConditionExpression;
                        var driveConditionExpression = JoinAnd(new[] { gatedSourceConditionExpression, interlockExpression });
                        var instruction = ResolveOutputInstruction(commandGroup.Select(item => item.Source.Qualifier));
                        var expression = BuildInstructionExpression(driveConditionExpression, instruction, physicalOutputRef);
                        var feedbackSignals = commandGroup
                            .SelectMany(item => item.Source.FeedbackSignals)
                            .GroupBy(signal => $"{signal.SignalName}\u001F{signal.PhysicalAddress}", StringComparer.OrdinalIgnoreCase)
                            .Select(signalGroup => signalGroup.First())
                            .ToList();
                        var mnemonicLines = EmitRungLines(driveConditionExpression, instruction, physicalOutputRef, variables);
                        var mnemonic = JoinMnemonicLines(mnemonicLines);
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
                var doneGuardExpression = NegateExpression(source.SourceDoneBitRef);
                var conditionExpression = JoinAnd(new[] { modeFlagAddress, executeExpression, doneGuardExpression });

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
                var conditionMnemonicLines = EmitConditionLines(command.conditionExpression, Array.Empty<DeviceVariable>());
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
                    conditionMnemonic = JoinMnemonicLines(conditionMnemonicLines),
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

        return IsFalseState((requiredState ?? string.Empty).Trim())
            ? NegateExpression(interlockAddress)
            : interlockAddress.Trim();
    }

    private static string ResolveOutputInstruction(IEnumerable<string?> qualifiers)
    {
        var first = qualifiers.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        return string.IsNullOrWhiteSpace(first) ? "OUT" : ResolveActionInstruction(first!);
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

    private static string JoinByAggregationMode(IEnumerable<string?> expressions, string? aggregationMode)
    {
        var values = expressions
            .Where(expression => !string.IsNullOrWhiteSpace(expression) && !string.Equals(expression.Trim(), "1", StringComparison.OrdinalIgnoreCase))
            .Select(expression => expression!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (values.Count == 0) return string.Empty;
        if (values.Count == 1) return values[0];

        var separator = string.Equals(aggregationMode, "AND", StringComparison.OrdinalIgnoreCase) ? " & " : " | ";
        return string.Join(separator, values.Select(WrapCompoundExpression));
    }

    private static string WrapCompoundExpression(string expression)
    {
        var value = expression.Trim();
        if (value.StartsWith("(", StringComparison.Ordinal) && value.EndsWith(")", StringComparison.Ordinal)) return value;
        return value.Contains(" & ", StringComparison.Ordinal) || value.Contains(" | ", StringComparison.Ordinal)
            ? $"({value})"
            : value;
    }
    private static string WrapConditionTerm(string expression)
    {
        var value = expression.Trim();
        return string.IsNullOrWhiteSpace(value) || IsSimpleOperand(value)
            ? value
            : $"({value})";
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


