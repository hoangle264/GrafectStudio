using Sprache;

namespace GrafcetStudio.App.Expressions;

public static class LogicExpressionParser
{
    private const string Grammar = "Expr := OrExpr; OrExpr := AndExpr ('|' AndExpr)*; AndExpr := Unary ('&' Unary)*; Unary := '!' Unary | Primary; Primary := TAG | '(' Expr ')'";

    public static string GrammarDefinition => Grammar;

    public static LogicExpression Parse(string expr, Parser<string> tagTokenParser, LogicExpressionParserOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(tagTokenParser);

        options ??= new LogicExpressionParserOptions();
        if (string.IsNullOrWhiteSpace(expr))
        {
            throw new LogicExpressionParseException($"Invalid {options.ExpressionDescription} at position 0: expression is empty. Grammar: {Grammar}");
        }

        try
        {
            return BuildExpressionParser(tagTokenParser).Parse(expr).Normalize();
        }
        catch (ParseException ex)
        {
            throw new LogicExpressionParseException(BuildFriendlyMessage(expr, ex, options), ex);
        }
    }

    private static Parser<LogicExpression> BuildExpressionParser(Parser<string> tagTokenParser)
    {
        Parser<LogicExpression> orExpression = default!;
        Parser<LogicExpression> andExpression = default!;
        Parser<LogicExpression> unary = default!;
        Parser<LogicExpression> primary = default!;
        Parser<LogicExpression> parenthesizedExpression = default!;

        var tagToken = tagTokenParser.Token();
        var expression = Sprache.Parse.Ref(() => orExpression).End();
        orExpression = Chain(Sprache.Parse.Char('|').Token(), Sprache.Parse.Ref(() => andExpression), "OR");
        andExpression = Chain(Sprache.Parse.Char('&').Token(), Sprache.Parse.Ref(() => unary), "AND");
        unary =
            (from not in Sprache.Parse.Char('!').Token()
             from operand in Sprache.Parse.Ref(() => unary)
             select LogicExpressionUtilities.CreateNot(operand))
            .Or(Sprache.Parse.Ref(() => primary));
        primary = Sprache.Parse.Ref(() => parenthesizedExpression)
            .Or(tagToken.Select(tag => LogicExpressionUtilities.CreateTag(tag)));
        parenthesizedExpression =
            from open in Sprache.Parse.Char('(').Token()
            from expr in Sprache.Parse.Ref(() => orExpression)
            from close in Sprache.Parse.Char(')').Token()
            select expr;

        return expression;
    }

    private static Parser<LogicExpression> Chain(Parser<char> opParser, Parser<LogicExpression> operandParser, string nodeType)
        => from first in operandParser
           from rest in (from op in opParser from operand in operandParser select operand).Many()
           select LogicExpressionUtilities.CreateLogical(nodeType, Enumerable.Repeat(first, 1).Concat(rest));

    private static string BuildFriendlyMessage(string expr, ParseException ex, LogicExpressionParserOptions options)
    {
        var position = NormalizeErrorPosition(expr, FindErrorPosition(expr, ex.Message));
        var detail = InferDetail(expr, position, options.TagDescription);
        return $"Invalid {options.ExpressionDescription} at position {position}: {detail}. Grammar: {Grammar}";
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

    private static string InferDetail(string expr, int position, string tagDescription)
    {
        if (position >= expr.Length)
        {
            var trimmed = expr.TrimEnd();
            var lastToken = trimmed.LastOrDefault();
            if (lastToken is '&' or '|')
            {
                return "unexpected end of expression; expected a tag, '!', or '('";
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
                ? "unexpected ')'; expected a tag, '!', or '(' after '('"
                : "unexpected ')'; expected '&', '|', or end of expression";
        }

        if (ch == '(')
        {
            return "unexpected '('; expected '&', '|', or end of expression";
        }

        if (ch == '&' || ch == '|')
        {
            return $"unexpected '{ch}'; expected a tag, '!', or '('";
        }

        if (char.IsWhiteSpace(ch))
        {
            var nextNonWhitespace = FindNextNonWhitespace(expr, position);
            if (nextNonWhitespace < expr.Length)
            {
                return InferDetail(expr, nextNonWhitespace, tagDescription);
            }

            return "unexpected end of expression";
        }

        return $"unexpected '{ch}'; expected a valid tag character ({tagDescription}), '!', '&', '|', or ')'";
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

