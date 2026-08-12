using System;
using System.Collections.Generic;
using System.Text;

namespace GrafcetStudio.App.Generators.Robot.RapidAst;

public class RapidLexer
{
    private static readonly HashSet<string> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "IF", "THEN", "ELSIF", "ELSE", "END_IF", "ENDIF",
        "WHILE", "DO", "ENDWHILE",
        "FOR", "FROM", "TO", "STEP", "ENDFOR",
        "TEST", "CASE", "DEFAULT", "ENDTEST",
        "GOTO", "PROC", "ENDPROC", "MODULE", "ENDMODULE",
        "AND", "OR", "NOT", "TRUE", "FALSE"
    };

    public static List<RapidToken> Tokenize(string source)
    {
        var tokens = new List<RapidToken>();
        if (string.IsNullOrEmpty(source))
        {
            tokens.Add(new RapidToken(RapidTokenType.EOF, "", 1, 1));
            return tokens;
        }

        int pos = 0;
        int line = 1;
        int col = 1;

        while (pos < source.Length)
        {
            char c = source[pos];

            // Comments
            if (c == '!')
            {
                while (pos < source.Length && source[pos] != '\n')
                {
                    pos++;
                    col++;
                }
                continue;
            }

            // Whitespace
            if (char.IsWhiteSpace(c))
            {
                if (c == '\n')
                {
                    line++;
                    col = 1;
                }
                else
                {
                    col++;
                }
                pos++;
                continue;
            }

            // Punctuation & Operators
            if (c == ':')
            {
                if (pos + 1 < source.Length && source[pos + 1] == '=')
                {
                    tokens.Add(new RapidToken(RapidTokenType.Assign, ":=", line, col));
                    pos += 2;
                    col += 2;
                }
                else
                {
                    tokens.Add(new RapidToken(RapidTokenType.Colon, ":", line, col));
                    pos++;
                    col++;
                }
                continue;
            }

            if (c == ';')
            {
                tokens.Add(new RapidToken(RapidTokenType.Semicolon, ";", line, col));
                pos++;
                col++;
                continue;
            }

            if (c == ',')
            {
                tokens.Add(new RapidToken(RapidTokenType.Comma, ",", line, col));
                pos++;
                col++;
                continue;
            }

            if (c == '(')
            {
                tokens.Add(new RapidToken(RapidTokenType.OpenParen, "(", line, col));
                pos++;
                col++;
                continue;
            }

            if (c == ')')
            {
                tokens.Add(new RapidToken(RapidTokenType.CloseParen, ")", line, col));
                pos++;
                col++;
                continue;
            }

            if (c == '=')
            {
                tokens.Add(new RapidToken(RapidTokenType.Equals, "=", line, col));
                pos++;
                col++;
                continue;
            }

            if (c == '<')
            {
                if (pos + 1 < source.Length && source[pos + 1] == '>')
                {
                    tokens.Add(new RapidToken(RapidTokenType.NotEquals, "<>", line, col));
                    pos += 2;
                    col += 2;
                }
                else
                {
                    tokens.Add(new RapidToken(RapidTokenType.LessThan, "<", line, col));
                    pos++;
                    col++;
                }
                continue;
            }

            if (c == '>')
            {
                tokens.Add(new RapidToken(RapidTokenType.GreaterThan, ">", line, col));
                pos++;
                col++;
                continue;
            }

            // String literals
            if (c == '"')
            {
                int startCol = col;
                var sb = new StringBuilder();
                pos++;
                col++;
                while (pos < source.Length && source[pos] != '"')
                {
                    sb.Append(source[pos]);
                    pos++;
                    col++;
                }
                if (pos < source.Length && source[pos] == '"')
                {
                    pos++;
                    col++;
                }
                tokens.Add(new RapidToken(RapidTokenType.StringLiteral, sb.ToString(), line, startCol));
                continue;
            }

            // Numbers
            if (char.IsDigit(c) || (c == '-' && pos + 1 < source.Length && char.IsDigit(source[pos + 1])))
            {
                int startCol = col;
                var sb = new StringBuilder();
                sb.Append(c);
                pos++;
                col++;
                while (pos < source.Length && (char.IsDigit(source[pos]) || source[pos] == '.' || source[pos] == 'E' || source[pos] == 'e'))
                {
                    sb.Append(source[pos]);
                    pos++;
                    col++;
                }
                tokens.Add(new RapidToken(RapidTokenType.Number, sb.ToString(), line, startCol));
                continue;
            }

            // Identifiers / Keywords
            if (char.IsLetter(c) || c == '_')
            {
                int startCol = col;
                var sb = new StringBuilder();
                while (pos < source.Length && (char.IsLetterOrDigit(source[pos]) || source[pos] == '_'))
                {
                    sb.Append(source[pos]);
                    pos++;
                    col++;
                }
                var val = sb.ToString();
                if (Keywords.Contains(val))
                {
                    tokens.Add(new RapidToken(RapidTokenType.Keyword, val.ToUpperInvariant(), line, startCol));
                }
                else
                {
                    tokens.Add(new RapidToken(RapidTokenType.Identifier, val, line, startCol));
                }
                continue;
            }

            // Skip unknown chars
            pos++;
            col++;
        }

        tokens.Add(new RapidToken(RapidTokenType.EOF, "", line, col));
        return tokens;
    }
}
