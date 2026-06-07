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
}
