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

        var keyenceInstruction = ToInstruction(instruction, target, vars);
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

    public static KeyenceInstruction? ToInstruction(string instruction, string target, IList<DeviceVariable> vars)
    {
        if (string.IsNullOrWhiteSpace(instruction) || string.IsNullOrWhiteSpace(target)) return null;

        var resolvedTarget = AddressResolver.Resolve(target, vars.ToList());
        return instruction.Trim().ToUpperInvariant() switch
        {
            "OUT" => KeyenceOutputInstruction.Create(KeyenceInstructionType.Out, resolvedTarget),
            "SET" => KeyenceOutputInstruction.Create(KeyenceInstructionType.Set, resolvedTarget),
            "RST" => KeyenceOutputInstruction.Create(KeyenceInstructionType.Rst, resolvedTarget),
            "RES" => KeyenceOutputInstruction.Create(KeyenceInstructionType.Res, resolvedTarget),
            _ => null
        };
    }

    public static string ConvertPseudoExpressions(string rendered, IList<DeviceVariable> vars)
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

    public static bool TryConvertPseudoExpressionLine(string? rawLine, IList<DeviceVariable> vars, out IList<string> mnemonicLines)
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

        mnemonicLines = EmitRungLines(condition, instruction, target, vars);
        return mnemonicLines.Count > 0;
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
