using System.Collections.Generic;
using GrafcetStudio.Domain.Enums;

namespace GrafcetStudio.Domain.Models;

/// <summary>Template-friendly step enriched with sequence and transition context.</summary>
public class ResolvedStep
{
    public int Index { get; init; }

    public bool IsFirst { get; init; }

    public Step Step { get; init; } = new();

    public Step? PreviousStep { get; init; }

    public Step? NextStep { get; init; }

    public Transition? InTransition { get; init; }

    public Transition? OutTransition { get; init; }

    public BranchType BranchType { get; init; }

    public string StepId => Step.Id;

    public int StepNumber => Step.Number;

    public string StepLabel => Step.Label;

    public bool IsInitial => Step.IsInitial;

    public string? ExecAddress => Step.ExecAddress;

    public string? DoneAddress => Step.DoneAddress;

    public string Kind => Step.Kind;

    public string? MacroFlowId => Step.MacroFlowId;

    public bool IsMacroStepCallPoint => string.Equals(Step.Kind, "macro", System.StringComparison.OrdinalIgnoreCase);

    public MacroBindingContext? MacroBinding { get; init; }

    public string MacroPortName => MacroBinding?.portName ?? string.Empty;

    public IList<StepAction> Actions => Step.Actions;

    public string? PreviousStepId => PreviousStep?.Id;

    public int? PreviousStepNumber => PreviousStep?.Number;

    public string? PreviousStepLabel => PreviousStep?.Label;

    public string? NextStepId => NextStep?.Id;

    public int? NextStepNumber => NextStep?.Number;

    public string? NextStepLabel => NextStep?.Label;

    public string? InTransitionId => InTransition?.Id;

    public string? InTransitionLabel => InTransition?.Label;

    public string? InTransitionCondition => InTransition?.Condition;

    public string? OutTransitionId => OutTransition?.Id;

    public string? OutTransitionLabel => OutTransition?.Label;

    public string? OutTransitionCondition => OutTransition?.Condition;

    public StepExpressionContext Expression { get; init; } = new();

    public StepExpressionContext expression => Expression;

    public string ConditionExpression => Expression.conditionExpression;

    public string ActivationExpression => Expression.activationExpression;

    public string HoldExpression => Expression.holdExpression;

    public string DoneExpression => Expression.doneExpression;

    public string BodyExpression => Expression.bodyExpression;

    public string ActivationMnemonic => Expression.activationMnemonic;

    public string DoneMnemonic => Expression.doneMnemonic;

    public string BodyMnemonic => Expression.bodyMnemonic;

    public string OutputInstruction => Expression.outputInstruction;

    public string OutputTarget => Expression.outputTarget;

    public IList<string> BodyExpressions => Expression.bodyExpressions;

    public IList<StepActionExpressionContext> ActionExpressions => Expression.actions;

    public IList<StepOutputExpressionContext> OutputExpressions => Expression.outputs;
}

public class StepExpressionContext
{
    public string contractVersion { get; init; } = "step-expression-v1";

    public string conditionExpression { get; init; } = string.Empty;

    public string activationExpression { get; init; } = string.Empty;

    public string holdExpression { get; init; } = string.Empty;

    public string inTransitionExpression { get; init; } = string.Empty;

    public string outTransitionExpression { get; init; } = string.Empty;

    public string transitionExpression => outTransitionExpression;

    public string doneConditionExpression { get; init; } = string.Empty;

    public string doneInstruction { get; init; } = string.Empty;

    public string doneTarget { get; init; } = string.Empty;

    public string doneExpression { get; init; } = string.Empty;

    public string bodyExpression { get; init; } = string.Empty;

    public string outputInstruction { get; init; } = string.Empty;

    public string outputTarget { get; init; } = string.Empty;

    public string outputExpression { get; init; } = string.Empty;

    public string conditionMnemonic { get; init; } = string.Empty;

    public IList<string> conditionMnemonicLines { get; init; } = new List<string>();

    public string activationMnemonic { get; init; } = string.Empty;

    public IList<string> activationMnemonicLines { get; init; } = new List<string>();

    public string holdMnemonic { get; init; } = string.Empty;

    public IList<string> holdMnemonicLines { get; init; } = new List<string>();

    public string doneMnemonic { get; init; } = string.Empty;

    public IList<string> doneMnemonicLines { get; init; } = new List<string>();

    public IList<string> doneConditionMnemonicLines { get; init; } = new List<string>();

    public string outputMnemonic { get; init; } = string.Empty;

    public IList<string> outputMnemonicLines { get; init; } = new List<string>();

    public string bodyMnemonic { get; init; } = string.Empty;

    public IList<string> bodyMnemonicLines { get; init; } = new List<string>();

    public IList<string> bodyExpressions { get; init; } = new List<string>();

    public IList<string> bodyMnemonics { get; init; } = new List<string>();

    public IList<StepActionExpressionContext> actions { get; init; } = new List<StepActionExpressionContext>();

    public IList<StepOutputExpressionContext> outputs { get; init; } = new List<StepOutputExpressionContext>();

    public StepTransitionExpressionContext? inTransition { get; init; }

    public StepTransitionExpressionContext? outTransition { get; init; }

    public bool hasActions => actions.Count > 0;

    public bool hasOutputs => outputs.Count > 0;

    public bool hasTransition => !string.IsNullOrWhiteSpace(outTransitionExpression);
}

public class StepActionExpressionContext
{
    public int index { get; init; }

    public int number { get; init; }

    public string variable { get; init; } = string.Empty;

    public string address { get; init; } = string.Empty;

    public string qualifier { get; init; } = string.Empty;

    public double timeMs { get; init; }

    public string conditionExpression { get; init; } = string.Empty;

    public string instruction { get; init; } = string.Empty;

    public string target { get; init; } = string.Empty;

    public string expression { get; init; } = string.Empty;

    public string mnemonic { get; init; } = string.Empty;

    public IList<string> mnemonicLines { get; init; } = new List<string>();

    public string completionExpression { get; init; } = string.Empty;

    public StepCompletionExpressionContext? completion { get; init; }

    public bool hasCompletion => completion is not null || !string.IsNullOrWhiteSpace(completionExpression);
}

public class StepOutputExpressionContext
{
    public int index { get; init; }

    public int number { get; init; }

    public string conditionExpression { get; init; } = string.Empty;

    public string instruction { get; init; } = "OUT";

    public string target { get; init; } = string.Empty;

    public string expression { get; init; } = string.Empty;

    public string mnemonic { get; init; } = string.Empty;

    public IList<string> mnemonicLines { get; init; } = new List<string>();

    public string deviceLabel { get; init; } = string.Empty;

    public string deviceFormat { get; init; } = string.Empty;

    public string commandId { get; init; } = string.Empty;

    public string actionLabel { get; init; } = string.Empty;

    public string driveSignal { get; init; } = string.Empty;

    public string interlockExpression { get; init; } = string.Empty;

    public IList<StepFeedbackExpressionContext> feedbackSignals { get; init; } = new List<StepFeedbackExpressionContext>();

    public bool hasInterlock => !string.IsNullOrWhiteSpace(interlockExpression);

    public bool hasFeedback => feedbackSignals.Count > 0;
}

public class StepTransitionExpressionContext
{
    public string id { get; init; } = string.Empty;

    public string label { get; init; } = string.Empty;

    public string condition { get; init; } = string.Empty;

    public string expression { get; init; } = string.Empty;

    public bool hasCondition => !string.IsNullOrWhiteSpace(expression);
}

public class StepCompletionExpressionContext
{
    public string sensor { get; init; } = string.Empty;

    public string sensorLabel { get; init; } = string.Empty;

    public string address { get; init; } = string.Empty;
}

public class StepFeedbackExpressionContext
{
    public string signalName { get; init; } = string.Empty;

    public string label { get; init; } = string.Empty;

    public string address { get; init; } = string.Empty;
}

