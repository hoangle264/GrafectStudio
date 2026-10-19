using GrafcetStudio.App.Expressions;
using Sprache;

namespace GrafcetStudio.App.Generators.Siemens;

public static class SiemensLadTagTokenParser
{
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

    public static Parser<string> TagToken { get; } = SiemensSymbolicTagToken
        .Or(PlainTagToken);

    public static LogicExpressionParserOptions ParserOptions { get; } = new()
    {
        ExpressionDescription = "ladder expression",
        TagDescription = TagDescription
    };
}
