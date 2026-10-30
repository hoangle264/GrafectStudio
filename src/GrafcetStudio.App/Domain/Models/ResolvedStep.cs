using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
    /// See <see cref="IncrementExecAddress"/> for the supported formats.
    /// Returns null if the current step has no ExecAddress.
    /// </summary>
    public string? NextExecAddress
        => NextStep?.ExecAddress ?? IncrementExecAddress(Step.ExecAddress);

    /// <summary>Lowercase alias for <see cref="NextExecAddress"/>. Used by Handlebars templates via <c>{{nextExecAddress}}</c>.</summary>
    public string? nextExecAddress => NextExecAddress;

    // "Tag".%X3 / Db.Member.%X3 (Siemens bit slice)
    private static readonly Regex SiemensBitSlicePattern = new(@"^(.*\.%X)(\d+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    // DM0.2, @DM10.3, ActiveWord.3 (word + bit)
    private static readonly Regex WordBitPattern = new(@"^(.+\.)(\d+)$", RegexOptions.CultureInvariant);
    // MR300, @MR300, Step_10, "Auto_S3" (trailing number, optional closing quote)
    private static readonly Regex TrailingNumberPattern = new(@"^(.*?)(\d+)(""?)$", RegexOptions.CultureInvariant);

    /// <summary>
    /// Increments an exec address or label by one, keeping its text, case and digit width:
    /// MR300 → MR301, DM0.2 → DM0.3, ActiveWord.3 → ActiveWord.4, "Tag".%X3 → "Tag".%X4,
    /// Auto_Step09 → Auto_Step10. Returns null when there is no number to increment.
    /// Bits are not rolled over into the next word.
    /// </summary>
    public static string? IncrementExecAddress(string? address)
    {
        if (string.IsNullOrWhiteSpace(address)) return null;

        var value = address.Trim();
        var match = SiemensBitSlicePattern.Match(value);
        if (!match.Success) match = WordBitPattern.Match(value);
        if (!match.Success) match = TrailingNumberPattern.Match(value);
        if (!match.Success) return null;

        var numberText = match.Groups[2].Value;
        if (!long.TryParse(numberText, out var number)) return null;

        var next = (number + 1).ToString().PadLeft(numberText.Length, '0');
        var suffix = match.Groups.Count > 3 ? match.Groups[3].Value : string.Empty;
        return match.Groups[1].Value + next + suffix;
    }

    public string? InTransitionId => InTransition?.Id;

    public string? InTransitionLabel => InTransition?.Label;

    public string? InTransitionCondition => InTransition?.Condition;

    public string? OutTransitionId => OutTransition?.Id;

    public string? OutTransitionLabel => OutTransition?.Label;

    public string? OutTransitionCondition => OutTransition?.Condition;
}
