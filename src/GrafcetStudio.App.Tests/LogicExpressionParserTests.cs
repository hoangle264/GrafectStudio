using GrafcetStudio.App.Expressions;
using GrafcetStudio.App.Generators.Siemens;
using Xunit;

namespace GrafcetStudio.App.Tests;

public class LogicExpressionParserTests
{
    [Theory]
    [InlineData("A", "TAG(A)")]
    [InlineData("A&B", "AND(TAG(A),TAG(B))")]
    [InlineData("A|B", "OR(TAG(A),TAG(B))")]
    [InlineData("a", "TAG(a)")]
    [InlineData("a & b", "AND(TAG(a),TAG(b))")]
    [InlineData("a | b", "OR(TAG(a),TAG(b))")]
    [InlineData("((a))", "TAG(a)")]
    [InlineData("(a | b) & c", "AND(OR(TAG(a),TAG(b)),TAG(c))")]
    [InlineData("a | b & c", "OR(TAG(a),AND(TAG(b),TAG(c)))")]
    [InlineData("a & b | c", "OR(AND(TAG(a),TAG(b)),TAG(c))")]
    [InlineData("a & (b | c) | d", "OR(AND(TAG(a),OR(TAG(b),TAG(c))),TAG(d))")]
    [InlineData("((A&B)|(C&D))&E", "AND(OR(AND(TAG(A),TAG(B)),AND(TAG(C),TAG(D))),TAG(E))")]
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
    [InlineData("A|", 2, "unexpected end of expression; expected a tag or '('")]
    [InlineData("|A", 0, "unexpected '|'; expected a tag or '('")]
    [InlineData("(A&B", 4, "missing ')'")]
    [InlineData("(a | b", 6, "missing ')'")]
    [InlineData("a &", 3, "unexpected end of expression; expected a tag or '('")]
    [InlineData("A(B)", 1, "unexpected '('; expected '&', '|', or end of expression")]
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

    [Fact]
    public void Normalize_FlattensNestedAndWhilePreservingOrGrouping()
    {
        var expression = LogicExpressionUtilities.CreateLogical("AND", new[]
        {
            LogicExpressionUtilities.CreateTag("A"),
            LogicExpressionUtilities.CreateLogical("AND", new[]
            {
                LogicExpressionUtilities.CreateTag("B"),
                LogicExpressionUtilities.CreateTag("C")
            }),
            LogicExpressionUtilities.CreateLogical("OR", new[]
            {
                LogicExpressionUtilities.CreateTag("D"),
                LogicExpressionUtilities.CreateTag("E")
            })
        });

        var normalized = expression.Normalize();

        Assert.Equal("AND(TAG(A),TAG(B),TAG(C),OR(TAG(D),TAG(E)))", ToDebug(normalized));
    }

    [Fact]
    public void Normalize_FlattensNestedOrWhilePreservingAndGrouping()
    {
        var expression = LogicExpressionUtilities.CreateLogical("OR", new[]
        {
            LogicExpressionUtilities.CreateTag("A"),
            LogicExpressionUtilities.CreateLogical("OR", new[]
            {
                LogicExpressionUtilities.CreateTag("B"),
                LogicExpressionUtilities.CreateTag("C")
            }),
            LogicExpressionUtilities.CreateLogical("AND", new[]
            {
                LogicExpressionUtilities.CreateTag("D"),
                LogicExpressionUtilities.CreateTag("E")
            })
        });

        var normalized = expression.Normalize();

        Assert.Equal("OR(TAG(A),TAG(B),TAG(C),AND(TAG(D),TAG(E)))", ToDebug(normalized));
    }

    [Fact]
    public void CollectRefs_WalksNestedExpressionTree()
    {
        var expression = LogicExpressionUtilities.CreateLogical("AND", new[]
        {
            LogicExpressionUtilities.CreateTag("A"),
            LogicExpressionUtilities.CreateLogical("OR", new[]
            {
                LogicExpressionUtilities.CreateTag("B"),
                LogicExpressionUtilities.CreateTag("C")
            })
        });
        var refs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        LogicExpressionUtilities.CollectRefs(expression, refs);

        Assert.Equal(new[] { "A", "B", "C" }, refs.OrderBy(item => item, StringComparer.OrdinalIgnoreCase));
    }

    private static LogicExpression ParseSiemens(string expr)
        => LogicExpressionParser.Parse(expr, SiemensLadTagTokenParser.TagToken, SiemensLadTagTokenParser.ParserOptions);

    private static string ToDebug(LogicExpression expression)
    {
        if (string.Equals(expression.Type, "TAG", StringComparison.OrdinalIgnoreCase))
        {
            return $"TAG({expression.Ref})";
        }

        return $"{expression.Type}({string.Join(",", expression.Nodes.ConvertAll(ToDebug))})";
    }
}
