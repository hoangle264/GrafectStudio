using GrafcetStudio.Domain.Enums;
using GrafcetStudio.Domain.Models;
using System;
using System.Collections.Generic;

namespace GrafcetStudio.App.Generators.Keyence;

/// <summary>
/// Intermediate instruction model for Keyence mnemonic generation.
/// This separates expression emission from final instruction selection, so future
/// instructions (timer, move, call, custom mnemonics, etc.) can be added without
/// changing the shared expression/parser layer.
/// </summary>
public abstract record KeyenceInstruction
{
    public abstract KeyenceInstructionType InstructionType { get; }

    /// <summary>Optional free-form comment or label associated with the emitted instruction.</summary>
    public string? Comment { get; init; }

    /// <summary>Optional extensibility bag for future instruction-specific parameters.</summary>
    public IReadOnlyDictionary<string, string> Parameters { get; init; } = EmptyParameters.Instance;

    protected static class EmptyParameters
    {
        public static readonly IReadOnlyDictionary<string, string> Instance = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}

public enum KeyenceInstructionType
{
    Out,
    Set,
    Rst,
    Res,
    Fb,
    Custom
}

/// <summary>
/// Base class for output-like instructions that target a single operand/address.
/// Supports current single-target flow while keeping room for future metadata.
/// </summary>
public abstract record KeyenceOutputInstruction : KeyenceInstruction
{
    public string TargetRef { get; init; } = string.Empty;

    /// <summary>Optional symbolic source reference used to derive TargetRef.</summary>
    public string? SourceRef { get; init; }

    public static KeyenceOutputInstruction Create(
        KeyenceInstructionType instructionType,
        string targetRef,
        string? sourceRef = null,
        string? comment = null,
        IReadOnlyDictionary<string, string>? parameters = null,
        string? blockName = null,
        string? body = null)
        => instructionType switch
        {
            KeyenceInstructionType.Out => new KeyenceCoilInstruction
            {
                TargetRef = targetRef,
                SourceRef = sourceRef,
                Comment = comment,
                Parameters = parameters ?? EmptyParameters.Instance
            },
            KeyenceInstructionType.Set => new KeyenceSetInstruction
            {
                TargetRef = targetRef,
                SourceRef = sourceRef,
                Comment = comment,
                Parameters = parameters ?? EmptyParameters.Instance
            },
            KeyenceInstructionType.Rst => new KeyenceResetInstruction
            {
                TargetRef = targetRef,
                SourceRef = sourceRef,
                Comment = comment,
                Parameters = parameters ?? EmptyParameters.Instance
            },
            KeyenceInstructionType.Res => new KeyenceResetByResInstruction
            {
                TargetRef = targetRef,
                SourceRef = sourceRef,
                Comment = comment,
                Parameters = parameters ?? EmptyParameters.Instance
            },
            KeyenceInstructionType.Fb => new KeyenceFunctionBlockInstruction
            {
                TargetRef = targetRef,
                SourceRef = sourceRef,
                Comment = comment,
                Parameters = parameters ?? EmptyParameters.Instance,
                BlockName = blockName,
                Body = body
            },
            KeyenceInstructionType.Custom => throw new ArgumentOutOfRangeException(nameof(instructionType), instructionType, "Use KeyenceCustomInstruction for custom mnemonics."),
            _ => throw new ArgumentOutOfRangeException(nameof(instructionType), instructionType, null)
        };

    public static KeyenceOutputInstruction FromAction(StepAction action, IReadOnlyList<DeviceVariable> vars, string? comment = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(vars);

        var targetRef = !string.IsNullOrWhiteSpace(action.Address)
            ? action.Address!
            : AddressResolver.Resolve(action.Variable, vars);

        return action.Qualifier switch
        {
            ActionQualifier.N => Create(KeyenceInstructionType.Out, targetRef, action.Variable, comment),
            ActionQualifier.S => Create(KeyenceInstructionType.Set, targetRef, action.Variable, comment),
            ActionQualifier.R => Create(KeyenceInstructionType.Rst, targetRef, action.Variable, comment),
            _ => new KeyenceUnsupportedOutputInstruction
            {
                TargetRef = targetRef,
                SourceRef = action.Variable,
                Comment = comment,
                Qualifier = action.Qualifier,
                Parameters = EmptyParameters.Instance
            }
        };
    }
}

public sealed record KeyenceCoilInstruction : KeyenceOutputInstruction
{
    public override KeyenceInstructionType InstructionType => KeyenceInstructionType.Out;
}

public sealed record KeyenceSetInstruction : KeyenceOutputInstruction
{
    public override KeyenceInstructionType InstructionType => KeyenceInstructionType.Set;
}

public sealed record KeyenceResetInstruction : KeyenceOutputInstruction
{
    public override KeyenceInstructionType InstructionType => KeyenceInstructionType.Rst;
}

/// <summary>
/// Represents the RES alias separately from RST so the abstraction preserves
/// intent even if the emission layer later chooses distinct syntax.
/// </summary>
public sealed record KeyenceResetByResInstruction : KeyenceOutputInstruction
{
    public override KeyenceInstructionType InstructionType => KeyenceInstructionType.Res;
}

/// <summary>
/// Placeholder abstraction for future function-block style instructions.
/// Phase 2 only defines the model; emission strategy is handled in later phases.
/// </summary>
public sealed record KeyenceFunctionBlockInstruction : KeyenceOutputInstruction
{
    public override KeyenceInstructionType InstructionType => KeyenceInstructionType.Fb;

    public string? BlockName { get; init; }

    /// <summary>Optional serialized body/metadata placeholder for future richer FB emission.</summary>
    public string? Body { get; init; }
}

public sealed record KeyenceUnsupportedOutputInstruction : KeyenceOutputInstruction
{
    public override KeyenceInstructionType InstructionType => KeyenceInstructionType.Custom;

    public ActionQualifier Qualifier { get; init; }
}

public sealed record KeyenceCustomInstruction : KeyenceInstruction
{
    public override KeyenceInstructionType InstructionType => KeyenceInstructionType.Custom;

    public string Mnemonic { get; init; } = string.Empty;

    public IReadOnlyList<string> Operands { get; init; } = Array.Empty<string>();

    /// <summary>Optional block/body content for future multi-line instructions.</summary>
    public string? Body { get; init; }
}
