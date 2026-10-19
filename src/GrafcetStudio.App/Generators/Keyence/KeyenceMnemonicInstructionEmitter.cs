using System;
using System.Text;

namespace GrafcetStudio.App.Generators.Keyence;

/// <summary>
/// Formats Keyence instruction abstractions into mnemonic lines.
/// Expression emission is intentionally separate; this class only knows how to
/// append the final output/custom instruction once a condition has been emitted.
/// </summary>
public static class KeyenceMnemonicInstructionEmitter
{
    public static string GetMnemonic(KeyenceInstruction instruction)
    {
        ArgumentNullException.ThrowIfNull(instruction);
        return GetMnemonic(instruction.InstructionType);
    }

    public static string GetMnemonic(KeyenceInstructionType instructionType)
        => instructionType switch
        {
            KeyenceInstructionType.Out => "OUT",
            KeyenceInstructionType.Set => "SET",
            KeyenceInstructionType.Rst => "RST",
            KeyenceInstructionType.Res => "RES",
            KeyenceInstructionType.Fb => "FB",
            KeyenceInstructionType.Custom => string.Empty,
            _ => throw new ArgumentOutOfRangeException(nameof(instructionType), instructionType, null)
        };

    public static string Format(KeyenceInstruction instruction, bool padTarget = false)
    {
        ArgumentNullException.ThrowIfNull(instruction);
        return instruction switch
        {
            KeyenceUnsupportedOutputInstruction unsupported => FormatUnsupported(unsupported),
            KeyenceOutputInstruction output => FormatOutput(output, padTarget),
            KeyenceCustomInstruction custom => FormatCustom(custom),
            _ => throw new NotSupportedException($"Unsupported Keyence instruction model '{instruction.GetType().Name}'.")
        };
    }

    public static void AppendLine(StringBuilder sb, KeyenceInstruction instruction, bool padTarget = false)
    {
        ArgumentNullException.ThrowIfNull(sb);
        sb.AppendLine(Format(instruction, padTarget));
    }

    private static string FormatOutput(KeyenceOutputInstruction instruction, bool padTarget)
    {
        var mnemonic = GetMnemonic(instruction.InstructionType);
        if (string.IsNullOrWhiteSpace(mnemonic))
        {
            throw new NotSupportedException($"Instruction type '{instruction.InstructionType}' does not have a built-in Keyence mnemonic.");
        }

        var target = padTarget ? instruction.TargetRef.PadRight(12) : instruction.TargetRef;
        var line = $"{mnemonic,-4} {target}";
        return string.IsNullOrWhiteSpace(instruction.Comment)
            ? line
            : $"{line}; {instruction.Comment}";
    }

    private static string FormatUnsupported(KeyenceUnsupportedOutputInstruction instruction)
        => $"; [{instruction.Qualifier}] {instruction.TargetRef} - not implemented";

    private static string FormatCustom(KeyenceCustomInstruction instruction)
    {
        if (string.IsNullOrWhiteSpace(instruction.Mnemonic))
        {
            throw new InvalidOperationException("Custom Keyence instruction requires a mnemonic.");
        }

        var operands = instruction.Operands.Count == 0
            ? string.Empty
            : " " + string.Join(" ", instruction.Operands);
        var line = $"{instruction.Mnemonic}{operands}";
        return string.IsNullOrWhiteSpace(instruction.Comment)
            ? line
            : $"{line}; {instruction.Comment}";
    }
}
