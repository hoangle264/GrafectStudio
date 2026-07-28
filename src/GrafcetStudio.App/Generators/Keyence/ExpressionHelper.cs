using GrafcetStudio.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GrafcetStudio.App.Generators.Keyence;

public static class ExpressionHelper
{
    public static string BuildConditionExpression(string? condition)
    {
        var value = (condition ?? string.Empty).Trim();
        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ? string.Empty : value;
    }

    public static string BuildActivationExpression(Step step, Step? previousStep, string inTransitionExpression, bool isFirstStep)
    {
        var terms = new List<string>();
        if (!isFirstStep && !step.IsInitial && !string.IsNullOrWhiteSpace(previousStep?.DoneAddress))
        {
            terms.Add(previousStep!.DoneAddress!);
        }

        AddConditionTerm(terms, inTransitionExpression);
        return JoinAnd(terms);
    }

    public static string BuildHoldExpression(Step step, string outTransitionExpression)
    {
        var terms = new List<string>();
        AddConditionTerm(terms, step.ExecAddress);
        AddConditionTerm(terms, NegateExpression(outTransitionExpression));
        return JoinAnd(terms);
    }


    public static string NegateExpression(string? expression)
    {
        if (string.IsNullOrWhiteSpace(expression)) return string.Empty;

        var value = expression.Trim();
        if (value.StartsWith("!", StringComparison.Ordinal))
        {
            return value[1..];
        }

        return IsSimpleOperand(value) ? $"!{value}" : $"!({value})";
    }

    public static string JoinAnd(IEnumerable<string?> terms)
        => string.Join(" & ", terms
            .Where(term => !string.IsNullOrWhiteSpace(term) && !string.Equals(term.Trim(), "1", StringComparison.OrdinalIgnoreCase))
            .Select(term => term!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase));

    public static string JoinByAggregationMode(IEnumerable<string?> expressions, string? aggregationMode)
    {
        var values = expressions
            .Where(expression => !string.IsNullOrWhiteSpace(expression) && !string.Equals(expression.Trim(), "1", StringComparison.OrdinalIgnoreCase))
            .Select(expression => expression!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (values.Count == 0) return string.Empty;
        if (values.Count == 1) return values[0];

        var separator = string.Equals(aggregationMode, "AND", StringComparison.OrdinalIgnoreCase) ? " & " : " | ";
        return string.Join(separator, values.Select(WrapCompoundExpression));
    }

    public static string WrapCompoundExpression(string expression)
    {
        var value = expression.Trim();
        if (value.StartsWith("(", StringComparison.Ordinal) && value.EndsWith(")", StringComparison.Ordinal)) return value;
        return value.Contains(" & ", StringComparison.Ordinal) || value.Contains(" | ", StringComparison.Ordinal)
            ? $"({value})"
            : value;
    }

    public static string WrapConditionTerm(string expression)
    {
        var value = expression.Trim();
        return string.IsNullOrWhiteSpace(value) || IsSimpleOperand(value)
            ? value
            : $"({value})";
    }

    public static void AddConditionTerm(IList<string> terms, string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || string.Equals(value.Trim(), "1", StringComparison.OrdinalIgnoreCase)) return;
        terms.Add(value.Trim());
    }

    public static bool IsFalseState(string value)
        => string.Equals(value, "0", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "false", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "off", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "low", StringComparison.OrdinalIgnoreCase);

    public static bool IsSimpleOperand(string expression)
    {
        var value = expression.Trim();
        if (string.IsNullOrWhiteSpace(value)) return false;

        if (value.StartsWith("CMP(", StringComparison.OrdinalIgnoreCase) && value.EndsWith(")", StringComparison.Ordinal))
        {
            return true;
        }

        if (value.Contains('&', StringComparison.Ordinal) || value.Contains('|', StringComparison.Ordinal) || value.Contains('(', StringComparison.Ordinal) || value.Contains(')', StringComparison.Ordinal) || value.Contains(' ', StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }
}
