using GrafcetStudio.CodeGen.Runtime.Models;
using GrafcetStudio.Domain.Models;
using GrafcetStudio.Domain.Resolution;
using System.Collections.Generic;
using System.Linq;

namespace GrafcetStudio.App.Generators.Keyence;

/// <summary>
/// Builds expression and mnemonic context objects for steps, actions, outputs, transitions, and completions.
/// All methods are pure functions with no side effects.
/// </summary>
internal static class StepContextBuilder
{
    /// <summary>
    /// Builds the full <see cref="StepExpressionContext"/> for a resolved step.
    /// </summary>
    public static StepExpressionContext BuildStepExpressionContext(
        Step step,
        Step? previousStep,
        Step? nextStep,
        Transition? inTransition,
        Transition? outTransition,
        bool isFirstStep,
        IList<DeviceVariable> variables)
    {
        var inTransitionExpression = ExpressionHelper.BuildConditionExpression(inTransition?.Condition);
        var outTransitionExpression = ExpressionHelper.BuildConditionExpression(outTransition?.Condition);
        var activationExpression = ExpressionHelper.BuildActivationExpression(step, previousStep, inTransitionExpression, isFirstStep);
        var holdExpression = ExpressionHelper.BuildHoldExpression(step, outTransitionExpression);
        var actions = BuildStepActionExpressions(step, variables);
        var outputs = BuildStepOutputExpressions(step, variables);
        var doneConditionExpression = ExpressionHelper.BuildDoneConditionExpression(step, outTransitionExpression, actions);
        var doneInstruction = string.IsNullOrWhiteSpace(step.DoneAddress) ? string.Empty : "SET";
        var doneTarget = step.DoneAddress ?? string.Empty;
        var activationMnemonicLines = MnemonicEmitter.EmitConditionLines(activationExpression, variables);
        var activationRungLines = MnemonicEmitter.EmitRungLines(activationExpression, "SET", step.ExecAddress ?? string.Empty, variables);
        var holdMnemonicLines = MnemonicEmitter.EmitConditionLines(holdExpression, variables);
        var doneConditionMnemonicLines = MnemonicEmitter.EmitConditionLines(doneConditionExpression, variables);
        var doneMnemonicLines = MnemonicEmitter.EmitRungLines(doneConditionExpression, doneInstruction, doneTarget, variables);
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
            conditionMnemonic = MnemonicEmitter.JoinMnemonicLines(activationMnemonicLines),
            conditionMnemonicLines = activationMnemonicLines,
            activationMnemonic = MnemonicEmitter.JoinMnemonicLines(activationRungLines),
            activationMnemonicLines = activationRungLines,
            holdMnemonic = MnemonicEmitter.JoinMnemonicLines(holdMnemonicLines),
            holdMnemonicLines = holdMnemonicLines,
            doneMnemonic = MnemonicEmitter.JoinMnemonicLines(doneMnemonicLines),
            doneMnemonicLines = doneMnemonicLines,
            doneConditionMnemonicLines = doneConditionMnemonicLines,
            outputMnemonic = MnemonicEmitter.JoinMnemonicBlocks(outputMnemonicBlocks),
            outputMnemonicLines = outputMnemonicLines,
            bodyMnemonic = MnemonicEmitter.JoinMnemonicBlocks(bodyMnemonics),
            bodyMnemonicLines = bodyMnemonics.SelectMany(block => MnemonicEmitter.SplitMnemonicLines(block)).ToList(),
            bodyExpressions = bodyExpressions,
            bodyMnemonics = bodyMnemonics,
            actions = actions,
            outputs = outputs,
            inTransition = BuildTransitionExpression(inTransition, inTransitionExpression),
            outTransition = BuildTransitionExpression(outTransition, outTransitionExpression)
        };
    }

    /// <summary>
    /// Builds action expression contexts (one per action on the step).
    /// </summary>
    public static IList<StepActionExpressionContext> BuildStepActionExpressions(
        Step step,
        IList<DeviceVariable> variables)
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
                    mnemonic = MnemonicEmitter.EmitRung(conditionExpression, instruction, target, variables),
                    mnemonicLines = MnemonicEmitter.EmitRungLines(conditionExpression, instruction, target, variables),
                    completionExpression = completionExpression,
                    completion = completion
                };
            })
            .ToList();

    /// <summary>
    /// Builds physical output expression contexts by expanding resolved command bindings.
    /// </summary>
    public static IList<StepOutputExpressionContext> BuildStepOutputExpressions(
        Step step,
        IList<DeviceVariable> variables)
    {
        var outputs = new List<StepOutputExpressionContext>();
        for (var actionIndex = 0; actionIndex < step.Actions.Count; actionIndex++)
        {
            var action = step.Actions[actionIndex];
            var resolved = action.ResolvedCommand;
            if (resolved is null || resolved.OutputBindings.Count == 0)
            {
                AddDirectActionOutput(outputs, action, actionIndex, step, variables);
                continue;
            }

            foreach (var binding in resolved.OutputBindings)
            {
                if (string.IsNullOrWhiteSpace(binding.PhysicalOutputRef)) continue;

                var interlockExpression = BuildInterlockExpression(binding);
                var conditionExpression = ExpressionHelper.JoinAnd(new[] { step.ExecAddress ?? string.Empty, interlockExpression });
                var expression = BuildInstructionExpression(conditionExpression, "OUT", binding.PhysicalOutputRef);
                outputs.Add(new StepOutputExpressionContext
                {
                    index = outputs.Count,
                    number = outputs.Count + 1,
                    conditionExpression = conditionExpression,
                    instruction = "OUT",
                    target = binding.PhysicalOutputRef,
                    expression = expression,
                    mnemonic = MnemonicEmitter.EmitRung(conditionExpression, "OUT", binding.PhysicalOutputRef, variables),
                    mnemonicLines = MnemonicEmitter.EmitRungLines(conditionExpression, "OUT", binding.PhysicalOutputRef, variables),
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

    /// <summary>
    /// Builds a transition expression context from an optional <see cref="Transition"/>.
    /// </summary>
    public static StepTransitionExpressionContext? BuildTransitionExpression(Transition? transition, string expression)
        => transition is null
            ? null
            : new StepTransitionExpressionContext
            {
                id = transition.Id,
                label = transition.Label,
                condition = transition.Condition,
                expression = expression
            };

    /// <summary>
    /// Builds a completion expression context from an optional <see cref="StepActionCompletion"/>.
    /// </summary>
    public static StepCompletionExpressionContext? BuildCompletionExpression(StepActionCompletion? completion)
        => completion is null
            ? null
            : new StepCompletionExpressionContext
            {
                sensor = completion.Sensor,
                sensorLabel = completion.SensorLabel,
                address = completion.Address
            };

    /// <summary>
    /// Resolves the physical address target for an action (direct address > variable lookup > variable name).
    /// </summary>
    public static string ResolveActionTarget(StepAction action, IList<DeviceVariable> variables)
    {
        if (!string.IsNullOrWhiteSpace(action.Address)) return action.Address!;

        var resolved = SignalResolver.ResolveAddress(action.Variable, variables);
        return string.IsNullOrWhiteSpace(resolved) ? action.Variable : resolved!;
    }

    /// <summary>
    /// Maps an IEC action qualifier string to a Keyence instruction mnemonic.
    /// </summary>
    public static string ResolveActionInstruction(string qualifier)
        => qualifier.ToUpperInvariant() switch
        {
            "S" or "SD" or "SL" => "SET",
            "R" => "RST",
            _ => "OUT"
        };

    /// <summary>
    /// Formats a combined instruction expression: "condition -> INST target" or "INST target".
    /// Returns empty if instruction or target is blank.
    /// </summary>
    public static string BuildInstructionExpression(string conditionExpression, string instruction, string target)
    {
        if (string.IsNullOrWhiteSpace(instruction) || string.IsNullOrWhiteSpace(target)) return string.Empty;

        return string.IsNullOrWhiteSpace(conditionExpression)
            ? $"{instruction} {target}"
            : $"{conditionExpression} -> {instruction} {target}";
    }

    /// <summary>
    /// Builds the interlock condition expression from an <see cref="OutputBinding"/>.
    /// Negates the address when the required state is false/0/off/low.
    /// </summary>
    public static string BuildInterlockExpression(OutputBinding binding)
    {
        if (string.IsNullOrWhiteSpace(binding.InterlockAddress)) return string.Empty;

        var requiredState = (binding.InterlockRequiredState ?? string.Empty).Trim();
        return ExpressionHelper.IsFalseState(requiredState)
            ? ExpressionHelper.NegateExpression(binding.InterlockAddress)
            : binding.InterlockAddress;
    }

    // ── Private helpers ────────────────────────────────────────────────────────

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
            mnemonic = MnemonicEmitter.EmitRung(conditionExpression, ResolveActionInstruction(action.Qualifier.ToString()), target, variables),
            mnemonicLines = MnemonicEmitter.EmitRungLines(conditionExpression, ResolveActionInstruction(action.Qualifier.ToString()), target, variables),
            commandId = action.Variable,
            actionLabel = action.Variable
        });
    }

    private static IList<string> BuildBodyExpressions(params string[] expressions)
        => expressions
            .Where(expression => !string.IsNullOrWhiteSpace(expression))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static IList<string> BuildBodyMnemonics(
        IList<string> activationRungLines,
        IList<StepOutputExpressionContext> outputs,
        IList<string> doneMnemonicLines)
    {
        var blocks = new List<string>();
        var activationBlock = MnemonicEmitter.JoinMnemonicLines(activationRungLines);
        if (!string.IsNullOrWhiteSpace(activationBlock)) blocks.Add(activationBlock);

        foreach (var output in outputs)
        {
            if (!string.IsNullOrWhiteSpace(output.mnemonic)) blocks.Add(output.mnemonic);
        }

        var doneBlock = MnemonicEmitter.JoinMnemonicLines(doneMnemonicLines);
        if (!string.IsNullOrWhiteSpace(doneBlock)) blocks.Add(doneBlock);
        return blocks
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
