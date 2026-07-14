using GrafcetStudio.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GrafcetStudio.App.Generators.Keyence;

public static class MnemonicEmitter
{
    public static string EmitRung(string condition, string instruction, string target, IList<DeviceVariable> vars)
        => JoinMnemonicLines(EmitRungLines(condition, instruction, target, vars));

    public static IList<string> EmitRungLines(string condition, string instruction, string target, IList<DeviceVariable> vars)
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

    public static IList<string> EmitConditionLines(string condition, IList<DeviceVariable> vars)
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

    public static KeyenceInstruction? ToInstruction(string instruction, string target)
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

    public static string JoinMnemonicLines(IEnumerable<string> lines)
        => string.Join(Environment.NewLine, lines.Where(line => !string.IsNullOrWhiteSpace(line)));

    public static string JoinMnemonicBlocks(IEnumerable<string> blocks)
        => string.Join(Environment.NewLine, blocks.Where(block => !string.IsNullOrWhiteSpace(block)));

    public static IList<string> SplitMnemonicLines(string text)
        => string.IsNullOrWhiteSpace(text)
            ? new List<string>()
            : text.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();

    private static string BuildInstructionExpression(string conditionExpression, string instruction, string target)
    {
        if (string.IsNullOrWhiteSpace(instruction) || string.IsNullOrWhiteSpace(target)) return string.Empty;

        return string.IsNullOrWhiteSpace(conditionExpression)
            ? $"{instruction} {target}"
            : $"{conditionExpression} -> {instruction} {target}";
    }
}
