using GrafcetStudio.App.Expressions;
using GrafcetStudio.Domain.Models;
using Sprache;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GrafcetStudio.App.Generators.Keyence;

/// <summary>
/// Controls how expression mnemonic lines connect to the current Keyence logic result.
/// </summary>
public enum KeyenceExpressionEmitMode
{
    /// <summary>Start a new logic result with LD/LDB.</summary>
    Load,

    /// <summary>AND the expression with a logic result that has already been loaded.</summary>
    AndWithCurrent
}

public sealed class KeyenceExpressionEmitOptions
{
    public KeyenceExpressionEmitMode Mode { get; init; } = KeyenceExpressionEmitMode.Load;

    /// <summary>Pad operand fields to match the legacy generator's 12-character alignment.</summary>
    public bool PadOperands { get; init; }
}

/// <summary>
/// Emits Keyence mnemonic logic for a shared <see cref="LogicExpression"/> AST.
///
/// The expression emitter is intentionally separate from
/// <see cref="KeyenceMnemonicInstructionEmitter"/>: this class builds the logic
/// leading up to an instruction, while the instruction emitter formats the final
/// OUT/SET/RST/RES/FB/custom line.
/// </summary>
public static class KeyenceMnemonicExpressionEmitter
{
    private const string KeyenceTagDescription = "letter, digit, '_', '.', '@', or '#'";

    public static Parser<string> TagToken { get; } = Sprache.Parse
        .Char(c => char.IsLetterOrDigit(c) || c is '_' or '.' or '@' or '#', KeyenceTagDescription)
        .AtLeastOnce()
        .Text();

    public static LogicExpressionParserOptions ParserOptions { get; } = new()
    {
        ExpressionDescription = "Keyence mnemonic expression",
        TagDescription = KeyenceTagDescription
    };

    public static LogicExpression Parse(string expression)
        => LogicExpressionParser.Parse(expression, TagToken, ParserOptions);

    public static IReadOnlyCollection<string> CollectRefs(LogicExpression? expression)
    {
        var refs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        LogicExpressionUtilities.CollectRefs(expression, refs);
        return refs;
    }

    public static IReadOnlyList<string> EmitCondition(
        string expression,
        IReadOnlyList<DeviceVariable> variables,
        KeyenceExpressionEmitOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(variables);
        return EmitCondition(Parse(expression), variables, options);
    }

    public static IReadOnlyList<string> EmitCondition(
        LogicExpression expression,
        IReadOnlyList<DeviceVariable> variables,
        KeyenceExpressionEmitOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(variables);
        return EmitCondition(expression, operand => AddressResolver.Resolve(operand, variables), options);
    }

    public static IReadOnlyList<string> EmitCondition(
        LogicExpression expression,
        Func<string, string> resolveOperand,
        KeyenceExpressionEmitOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(resolveOperand);

        options ??= new KeyenceExpressionEmitOptions();
        var lines = new List<string>();
        var normalized = expression.Normalize();

        if (options.Mode == KeyenceExpressionEmitMode.AndWithCurrent)
        {
            AppendAndWithCurrent(normalized, lines, resolveOperand, options);
        }
        else
        {
            EmitAsLoadedResult(normalized, lines, resolveOperand, options);
        }

        return lines;
    }

    public static void AppendCondition(
        StringBuilder sb,
        string expression,
        IReadOnlyList<DeviceVariable> variables,
        KeyenceExpressionEmitOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(sb);
        foreach (var line in EmitCondition(expression, variables, options))
        {
            sb.AppendLine(line);
        }
    }

    public static void AppendCondition(
        StringBuilder sb,
        LogicExpression expression,
        IReadOnlyList<DeviceVariable> variables,
        KeyenceExpressionEmitOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(sb);
        foreach (var line in EmitCondition(expression, variables, options))
        {
            sb.AppendLine(line);
        }
    }

    public static string EmitExpressionAndInstruction(
        string expression,
        IReadOnlyList<DeviceVariable> variables,
        KeyenceInstruction instruction,
        bool padInstructionTarget = false,
        KeyenceExpressionEmitOptions? options = null)
    {
        var sb = new StringBuilder();
        AppendExpressionAndInstruction(sb, expression, variables, instruction, padInstructionTarget, options);
        return sb.ToString();
    }

    public static void AppendExpressionAndInstruction(
        StringBuilder sb,
        string expression,
        IReadOnlyList<DeviceVariable> variables,
        KeyenceInstruction instruction,
        bool padInstructionTarget = false,
        KeyenceExpressionEmitOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(sb);
        ArgumentNullException.ThrowIfNull(instruction);

        AppendCondition(sb, expression, variables, options);
        KeyenceMnemonicInstructionEmitter.AppendLine(sb, instruction, padInstructionTarget);
    }

    private static void EmitAsLoadedResult(
        LogicExpression expression,
        ICollection<string> lines,
        Func<string, string> resolveOperand,
        KeyenceExpressionEmitOptions options)
    {
        expression = expression.Normalize();

        if (TryGetTag(expression, out var tag, out var negated))
        {
            AddOperandLine(lines, negated ? "LDB" : "LD", ResolveOperand(tag, resolveOperand), options);
            return;
        }

        switch (GetNodeType(expression))
        {
            case "AND":
                EmitConjunctionAsLoadedResult(expression, lines, resolveOperand, options);
                return;

            case "OR":
                EmitDisjunctionAsLoadedResult(expression, lines, resolveOperand, options);
                return;

            case "NOT":
                if (expression.Node is null)
                {
                    throw new NotSupportedException("Keyence NOT expression requires a child node.");
                }

                EmitAsLoadedResult(expression.Node, lines, resolveOperand, options);
                lines.Add("INV");
                return;

            default:
                throw UnsupportedNode(expression);
        }
    }

    private static void AppendAndWithCurrent(
        LogicExpression expression,
        ICollection<string> lines,
        Func<string, string> resolveOperand,
        KeyenceExpressionEmitOptions options)
    {
        expression = expression.Normalize();

        if (TryGetTag(expression, out var tag, out var negated))
        {
            AddOperandLine(lines, negated ? "ANB" : "AND", ResolveOperand(tag, resolveOperand), options);
            return;
        }

        if (GetNodeType(expression) == "AND")
        {
            foreach (var child in GetRequiredNodes(expression, "AND"))
            {
                AppendAndWithCurrent(child, lines, resolveOperand, options);
            }

            return;
        }

        EmitAsLoadedResult(expression, lines, resolveOperand, options);
        lines.Add("ANL");
    }

    private static void AppendOrWithCurrent(
        LogicExpression expression,
        ICollection<string> lines,
        Func<string, string> resolveOperand,
        KeyenceExpressionEmitOptions options)
    {
        expression = expression.Normalize();

        if (TryGetTag(expression, out var tag, out var negated))
        {
            AddOperandLine(lines, negated ? "ORB" : "OR", ResolveOperand(tag, resolveOperand), options);
            return;
        }

        if (GetNodeType(expression) == "OR")
        {
            foreach (var child in GetRequiredNodes(expression, "OR"))
            {
                AppendOrWithCurrent(child, lines, resolveOperand, options);
            }

            return;
        }

        EmitAsLoadedResult(expression, lines, resolveOperand, options);
        lines.Add("ORL");
    }

    private static void EmitConjunctionAsLoadedResult(
        LogicExpression expression,
        ICollection<string> lines,
        Func<string, string> resolveOperand,
        KeyenceExpressionEmitOptions options)
    {
        var nodes = GetRequiredNodes(expression, "AND");
        EmitAsLoadedResult(nodes[0], lines, resolveOperand, options);

        foreach (var child in nodes.Skip(1))
        {
            AppendAndWithCurrent(child, lines, resolveOperand, options);
        }
    }

    private static void EmitDisjunctionAsLoadedResult(
        LogicExpression expression,
        ICollection<string> lines,
        Func<string, string> resolveOperand,
        KeyenceExpressionEmitOptions options)
    {
        var nodes = GetRequiredNodes(expression, "OR");
        EmitAsLoadedResult(nodes[0], lines, resolveOperand, options);

        foreach (var child in nodes.Skip(1))
        {
            AppendOrWithCurrent(child, lines, resolveOperand, options);
        }
    }

    private static IReadOnlyList<LogicExpression> GetRequiredNodes(LogicExpression expression, string nodeType)
    {
        if (expression.Nodes.Count == 0)
        {
            throw new NotSupportedException($"Keyence {nodeType} expression requires at least one child node.");
        }

        return expression.Nodes;
    }

    private static bool TryGetTag(LogicExpression expression, out string tag, out bool negated)
    {
        if (GetNodeType(expression) == "TAG")
        {
            tag = expression.Ref ?? string.Empty;
            negated = expression.Negated;
            return true;
        }

        if (GetNodeType(expression) == "NOT" && expression.Node is not null && GetNodeType(expression.Node) == "TAG")
        {
            tag = expression.Node.Ref ?? string.Empty;
            negated = !expression.Node.Negated;
            return true;
        }

        tag = string.Empty;
        negated = false;
        return false;
    }

    private static string ResolveOperand(string tag, Func<string, string> resolveOperand)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            throw new NotSupportedException("Keyence TAG expression requires a non-empty ref.");
        }

        var resolved = resolveOperand(tag);
        return string.IsNullOrWhiteSpace(resolved) ? tag : resolved;
    }

    private static void AddOperandLine(
        ICollection<string> lines,
        string mnemonic,
        string operand,
        KeyenceExpressionEmitOptions options)
        => lines.Add($"{mnemonic,-4} {(options.PadOperands ? operand.PadRight(12) : operand)}");

    private static string GetNodeType(LogicExpression expression)
        => expression.Type?.Trim().ToUpperInvariant() ?? string.Empty;

    private static NotSupportedException UnsupportedNode(LogicExpression expression)
        => new($"Unsupported Keyence expression node '{expression.Type}'. Supported nodes: TAG, AND, OR, NOT.");
}
