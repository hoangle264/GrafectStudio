using GrafcetStudio.App.Expressions;
using GrafcetStudio.App.Generators.Siemens;
using Xunit;

namespace GrafcetStudio.App.Tests;

public class LogicExpressionParserTests
{
    [Theory]
    [InlineData("a", "TAG(a)")]
    [InlineData("a & b", "AND(TAG(a),TAG(b))")]
    [InlineData("a | b", "OR(TAG(a),TAG(b))")]
    [InlineData("((a))", "TAG(a)")]
    [InlineData("(a | b) & c", "AND(OR(TAG(a),TAG(b)),TAG(c))")]
    [InlineData("a | b & c", "OR(TAG(a),AND(TAG(b),TAG(c)))")]
    [InlineData("a & b | c", "OR(AND(TAG(a),TAG(b)),TAG(c))")]
    [InlineData("a & (b | c) | d", "OR(AND(TAG(a),OR(TAG(b),TAG(c))),TAG(d))")]
    [InlineData("segment.Powerrail & (contactParts[0] | coil1) & coil2", "AND(TAG(segment.Powerrail),OR(TAG(contactParts[0]),TAG(coil1)),TAG(coil2))")]
    [InlineData("contactParts[0] & segment.Powerrail", "AND(TAG(contactParts[0]),TAG(segment.Powerrail))")]
    [InlineData("foo.bar[12]", "TAG(foo.bar[12])")]
    [InlineData("  a\t&\r\n( b | c )  ", "AND(TAG(a),OR(TAG(b),TAG(c)))")]
    [InlineData("a & (b & c)", "AND(TAG(a),TAG(b),TAG(c))")]
    [InlineData("a | (b | c)", "OR(TAG(a),TAG(b),TAG(c))")]
    public void Parse_HappyPathAndPrecedence_ReturnsNormalizedAst(string expr, string expected)
    {
        var result = ParseSiemens(expr);

        Assert.Equal(expected, ToDebug(result));
    }

    [Theory]
    [InlineData("DB1.\"Motor State\".%X3", "TAG(DB1.\"Motor State\".%X3)")]
    [InlineData("\"Root DB\".Nested[0].Flag", "TAG(\"Root DB\".Nested[0].Flag)")]
    public void Parse_SiemensTagTokenGrammar_ReturnsTag(string expr, string expected)
    {
        var result = ParseSiemens(expr);

        Assert.Equal(expected, ToDebug(result));
    }

    [Theory]
    [InlineData("", 0, "expression is empty")]
    [InlineData("   ", 0, "expression is empty")]
    [InlineData("(a | b", 6, "missing ')'")]
    [InlineData("a &", 3, "unexpected end of expression; expected a tag or '('")]
    [InlineData("a $ b", 2, "unexpected '$'")]
    [InlineData("()", 1, "unexpected ')'; expected a tag or '(' after '('")]
    public void Parse_InvalidExpression_ThrowsHelpfulError(string expr, int expectedPosition, string expectedFragment)
    {
        var ex = Assert.Throws<LogicExpressionParseException>(() => ParseSiemens(expr));

        Assert.Contains("Invalid ladder expression", ex.Message);
        Assert.Contains($"position {expectedPosition}", ex.Message);
        Assert.Contains(expectedFragment, ex.Message);
        Assert.Contains("Grammar:", ex.Message);
    }

    private static LogicExpression ParseSiemens(string expr)
        => LogicExpressionParser.Parse(expr, SiemensLadTagTokenParser.TagToken, SiemensLadTagTokenParser.ParserOptions);

    private static string ToDebug(LogicExpression expression)
    {
        if (string.Equals(expression.Type, "TAG", System.StringComparison.OrdinalIgnoreCase))
        {
            return $"TAG({expression.Ref})";
        }

        return $"{expression.Type}({string.Join(",", expression.Nodes.ConvertAll(ToDebug))})";
    }
}
