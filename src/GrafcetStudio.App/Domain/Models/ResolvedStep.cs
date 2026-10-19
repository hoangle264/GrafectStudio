using System.Collections.Generic;
using System.Linq;
using GrafcetStudio.Domain.Enums;

namespace GrafcetStudio.Domain.Models;

/// <summary>Template-friendly step enriched with sequence and transition context.</summary>
public class ResolvedStep
{
    public int Index { get; init; }

    public bool IsFirst { get; init; }

    public bool IsLast { get; init; }

    public Step Step { get; init; } = new();

    public Step? PreviousStep { get; init; }

    public Step? NextStep { get; init; }

    public Transition? InTransition { get; init; }

    public Transition? OutTransition { get; init; }

    public BranchType BranchType { get; init; }

    public string StepId => Step.Id;

    public int StepNumber => Step.Number;

    public string StepLabel => Step.Label;

    /// <summary>Lowercase alias for <see cref="StepId"/>. Used by Handlebars templates via <c>{{id}}</c>.</summary>
    public string id => Step.Id;

    /// <summary>Lowercase alias for <see cref="StepNumber"/>. Used by Handlebars templates via <c>{{number}}</c>.</summary>
    public int number => Step.Number;

    /// <summary>Lowercase alias for <see cref="StepLabel"/>. Used by Handlebars templates via <c>{{label}}</c>.</summary>
    public string label => Step.Label;

    /// <summary>Lowercase alias for <see cref="ExecAddress"/>. Used by Handlebars templates via <c>{{execAddress}}</c>.</summary>
    public string? execAddress => Step.ExecAddress;

    /// <summary>Lowercase alias for <see cref="DoneAddress"/>. Used by Handlebars templates via <c>{{doneAddress}}</c>.</summary>
    public string? doneAddress => Step.DoneAddress;

    public bool IsInitial => Step.IsInitial;

    public string? ExecAddress => Step.ExecAddress;

    public string? DoneAddress => Step.DoneAddress;

    public string Kind => Step.Kind;

    public string? MacroFlowId => Step.MacroFlowId;

    public bool IsMacroStepCallPoint => string.Equals(Step.Kind, "macro", System.StringComparison.OrdinalIgnoreCase);

    public MacroBindingContext? MacroBinding { get; init; }

    public string MacroPortName => MacroBinding?.portName ?? string.Empty;

    /// <summary>Lowercase alias for <see cref="Actions"/>. Used by Handlebars templates via <c>{{#each actions}}</c>.</summary>
    public IList<StepAction> actions => Step.Actions;

    public IList<StepAction> Actions => Step.Actions;

    public string? PreviousStepId => PreviousStep?.Id;

    public int? PreviousStepNumber => PreviousStep?.Number;

    public string? PreviousStepLabel => PreviousStep?.Label;

    public string? NextStepId => NextStep?.Id;

    public int? NextStepNumber => NextStep?.Number;

    public string? NextStepLabel => NextStep?.Label;

    /// <summary>
    /// Returns the ExecAddress of the next step when available.
    /// For the last step in a flow (NextStep is null), automatically generates
    /// the address by incrementing the current step's ExecAddress by 1.
    /// Supports both plain numeric addresses (e.g. MR300 → MR301)
    /// and bit-addressed formats (e.g. DM0.2 → DM0.3).
    /// Returns null if the current step has no ExecAddress.
    /// </summary>
    public string? NextExecAddress
        => NextStep?.ExecAddress ?? IncrementExecAddress(Step.ExecAddress);

    /// <summary>Lowercase alias for <see cref="NextExecAddress"/>. Used by Handlebars templates via <c>{{nextExecAddress}}</c>.</summary>
    public string? nextExecAddress => NextExecAddress;

    private static string? IncrementExecAddress(string? address)
    {
        if (string.IsNullOrWhiteSpace(address)) return null;

        var trimmed = address.Trim().TrimStart('@');
        var dotIndex = trimmed.IndexOf('.');
        var head = dotIndex >= 0 ? trimmed[..dotIndex] : trimmed;
        var bitText = dotIndex >= 0 ? trimmed[(dotIndex + 1)..] : string.Empty;

        var prefixLength = head.TakeWhile(char.IsLetter).Count();
        if (prefixLength <= 0 || prefixLength >= head.Length) return null;

        var prefix = head[..prefixLength].ToUpperInvariant();
        var numberText = head[prefixLength..];
        if (!int.TryParse(numberText, out var number)) return null;

        var width = numberText.Length > 1 ? numberText.Length : 0;

        // Bit-addressed format: prefix + number + "." + bit  (e.g. DM0.2 → DM0.3)
        if (int.TryParse(bitText, out var bit))
        {
            var formatted = width > 0
                ? $"{prefix}{number.ToString().PadLeft(width, '0')}.{bit + 1}"
                : $"{prefix}{number}.{bit + 1}";
            return formatted;
        }

        // Plain numeric format: prefix + number  (e.g. MR300 → MR301)
        {
            var nextNumber = number + 1;
            return width > 0
                ? $"{prefix}{nextNumber.ToString().PadLeft(width, '0')}"
                : $"{prefix}{nextNumber}";
        }
    }

    public string? InTransitionId => InTransition?.Id;

    public string? InTransitionLabel => InTransition?.Label;

    public string? InTransitionCondition => InTransition?.Condition;

    public string? OutTransitionId => OutTransition?.Id;

    public string? OutTransitionLabel => OutTransition?.Label;

    public string? OutTransitionCondition => OutTransition?.Condition;
}
