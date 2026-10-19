namespace GrafcetStudio.App.Generators.Robot.RapidAst;

public enum RapidTokenType
{
    Keyword,
    Identifier,
    Number,
    StringLiteral,
    Colon,
    Semicolon,
    Comma,
    Assign,
    Equals,
    NotEquals,
    LessThan,
    GreaterThan,
    OpenParen,
    CloseParen,
    EOF
}

public class RapidToken
{
    public RapidTokenType Type { get; }
    public string Value { get; }
    public int Line { get; }
    public int Column { get; }

    public RapidToken(RapidTokenType type, string value, int line, int column)
    {
        Type = type;
        Value = value;
        Line = line;
        Column = column;
    }

    public override string ToString() => $"{Type}({Value}) at L{Line}:C{Column}";
}
