using System;
using System.Collections.Generic;
using System.Linq;
using Sprache;

namespace GrafcetStudio.App.Generators.Siemens;

public sealed class SiemensLadExpressionParseException : Exception
{
    public SiemensLadExpressionParseException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

public static class SiemensLadExpressionParser
{
    private const string Grammar = "Expr := OrExpr; OrExpr := AndExpr ('|' AndExpr)*; AndExpr := Primary ('&' Primary)*; Primary := TAG | '(' Expr ')'";
    private const string TagDescription = "letter, digit, '_', '.', '[', ']', double quote, or '%'";

    private static readonly Parser<string> SiemensQuotedComponent =
        from openQuote in Sprache.Parse.Char('"')
        from name in Sprache.Parse.CharExcept('"').AtLeastOnce().Text()
        from closeQuote in Sprache.Parse.Char('"')
        select $"\"{name}\"";

    private static readonly Parser<string> SiemensPlainComponent = Sprache.Parse.Char(c => char.IsLetterOrDigit(c) || c is '_' or '[' or ']', TagDescription)
        .AtLeastOnce()
        .Text();

    private static readonly Parser<string> SiemensSymbolicComponent = SiemensQuotedComponent
        .Or(SiemensPlainComponent);

    private static readonly Parser<string> SiemensBitSliceSuffix =
        from dot in Sprache.Parse.Char('.')
        from percent in Sprache.Parse.Char('%')
        from kind in Sprache.Parse.Chars('X', 'x')
        from bit in Sprache.Parse.Digit.AtLeastOnce().Text()
        select $".%{char.ToUpperInvariant(kind)}{bit}";

    private static readonly Parser<string> SiemensSymbolicTagToken =
        from first in SiemensSymbolicComponent
        from rest in (from dot in Sprache.Parse.Char('.') from component in SiemensSymbolicComponent select "." + component).Many()
        from bitSlice in SiemensBitSliceSuffix.Optional()
        select first + string.Concat(rest) + bitSlice.GetOrElse(string.Empty);

    private static readonly Parser<string> PlainTagToken = Sprache.Parse.Char(c => char.IsLetterOrDigit(c) || c is '_' or '.' or '[' or ']' or '%', TagDescription)
        .AtLeastOnce()
        .Text();

    private static readonly Parser<string> TagToken = SiemensSymbolicTagToken
        .Or(PlainTagToken)
        .Token();

    private static readonly Parser<SiemensLadExpression> Expression = Sprache.Parse.Ref(() => OrExpression).End();
    private static readonly Parser<SiemensLadExpression> OrExpression = Chain(Sprache.Parse.Char('|').Token(), Sprache.Parse.Ref(() => AndExpression), "OR");
    private static readonly Parser<SiemensLadExpression> AndExpression = Chain(Sprache.Parse.Char('&').Token(), Sprache.Parse.Ref(() => Primary), "AND");
    private static readonly Parser<SiemensLadExpression> Primary =
        Sprache.Parse.Ref(() => ParenthesizedExpression)
            .Or(TagToken.Select(CreateTag));

    private static readonly Parser<SiemensLadExpression> ParenthesizedExpression =
        from open in Sprache.Parse.Char('(').Token()
        from expr in Sprache.Parse.Ref(() => OrExpression)
        from close in Sprache.Parse.Char(')').Token()
        select expr;

    public static string GrammarDefinition => Grammar;

    public static SiemensLadExpression Parse(string expr)
    {
        if (string.IsNullOrWhiteSpace(expr))
        {
            throw new SiemensLadExpressionParseException($"Invalid ladder expression at position 0: expression is empty. Grammar: {Grammar}");
        }

        try
        {
            return Expression.Parse(expr).Normalize();
        }
        catch (ParseException ex)
        {
            throw new SiemensLadExpressionParseException(BuildFriendlyMessage(expr, ex), ex);
        }
    }

    private static Parser<SiemensLadExpression> Chain(Parser<char> opParser, Parser<SiemensLadExpression> operandParser, string nodeType)
        => from first in operandParser
           from rest in (from op in opParser from operand in operandParser select operand).Many()
           select CreateLogical(nodeType, Enumerable.Repeat(first, 1).Concat(rest));

    private static SiemensLadExpression CreateTag(string tag)
        => new()
        {
            Type = "TAG",
            Ref = tag
        };

    private static SiemensLadExpression CreateLogical(string nodeType, IEnumerable<SiemensLadExpression> nodes)
        => new()
        {
            Type = nodeType,
            Nodes = nodes.ToList()
        };

    private static SiemensLadExpression Normalize(this SiemensLadExpression expression)
    {
        var type = expression.Type?.Trim().ToUpperInvariant();
        return type switch
        {
            "TAG" => CreateTag(expression.Ref ?? string.Empty),
            "AND" => NormalizeLogical("AND", expression.Nodes),
            "OR" => NormalizeLogical("OR", expression.Nodes),
            _ => expression
        };
    }

    private static SiemensLadExpression NormalizeLogical(string nodeType, IEnumerable<SiemensLadExpression> nodes)
    {
        var normalizedChildren = nodes.Select(node => node.Normalize()).ToList();
        var flattened = new List<SiemensLadExpression>();
        foreach (var child in normalizedChildren)
        {
            if (string.Equals(child.Type, nodeType, StringComparison.OrdinalIgnoreCase))
            {
                flattened.AddRange(child.Nodes);
            }
            else
            {
                flattened.Add(child);
            }
        }

        if (flattened.Count == 1)
        {
            return flattened[0];
        }

        return CreateLogical(nodeType, flattened);
    }

    private static string BuildFriendlyMessage(string expr, ParseException ex)
    {
        var position = NormalizeErrorPosition(expr, FindErrorPosition(expr, ex.Message));
        var detail = InferDetail(expr, position);
        return $"Invalid ladder expression at position {position}: {detail}. Grammar: {Grammar}";
    }

    private static int FindErrorPosition(string expr, string? parseMessage)
    {
        if (!string.IsNullOrWhiteSpace(parseMessage))
        {
            var marker = "(Line 1, Column ";
            var start = parseMessage.IndexOf(marker, StringComparison.Ordinal);
            if (start >= 0)
            {
                start += marker.Length;
                var end = parseMessage.IndexOf(')', start);
                if (end > start && int.TryParse(parseMessage[start..end], out var column))
                {
                    return Math.Max(0, Math.Min(expr.Length, column - 1));
                }
            }
        }

        return expr.Length;
    }

    private static int NormalizeErrorPosition(string expr, int position)
    {
        if (position < expr.Length && (expr[position] == '&' || expr[position] == '|'))
        {
            var nextNonWhitespace = FindNextNonWhitespace(expr, position + 1);
            if (nextNonWhitespace >= expr.Length)
            {
                return expr.Length;
            }
        }

        return position;
    }

    private static string InferDetail(string expr, int position)
    {
        if (position >= expr.Length)
        {
            var trimmed = expr.TrimEnd();
            var lastToken = trimmed.LastOrDefault();
            if (lastToken is '&' or '|')
            {
                return "unexpected end of expression; expected a tag or '('";
            }

            if (HasUnclosedParenthesis(trimmed))
            {
                return "unexpected end of expression; missing ')'";
            }

            return lastToken switch
            {
                _ => "unexpected end of expression"
            };
        }

        var ch = expr[position];
        if (ch == ')')
        {
            return position > 0 && expr[position - 1] == '('
                ? "unexpected ')'; expected a tag or '(' after '('"
                : "unexpected ')'; expected '&', '|', or end of expression";
        }

        if (ch == '(')
        {
            return "unexpected '('; expected '&', '|', or end of expression";
        }

        if (ch == '&' || ch == '|')
        {
            return $"unexpected '{ch}'; expected a tag or '('";
        }

        if (char.IsWhiteSpace(ch))
        {
            var nextNonWhitespace = FindNextNonWhitespace(expr, position);
            if (nextNonWhitespace < expr.Length)
            {
                return InferDetail(expr, nextNonWhitespace);
            }

            return "unexpected end of expression";
        }

        return $"unexpected '{ch}'; expected a valid tag character ({TagDescription}), '&', '|', or ')'";
    }

    private static int FindNextNonWhitespace(string expr, int start)
    {
        var index = start;
        while (index < expr.Length && char.IsWhiteSpace(expr[index]))
        {
            index++;
        }

        return index;
    }

    private static bool HasUnclosedParenthesis(string expr)
    {
        var balance = 0;
        foreach (var ch in expr)
        {
            if (ch == '(')
            {
                balance++;
                continue;
            }

            if (ch == ')' && balance > 0)
            {
                balance--;
            }
        }

        return balance > 0;
    }
}
