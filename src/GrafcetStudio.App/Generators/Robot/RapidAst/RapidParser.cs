using System;
using System.Collections.Generic;
using System.Text;

namespace GrafcetStudio.App.Generators.Robot.RapidAst;

public class RapidParser
{
    private readonly List<RapidToken> _tokens;
    private int _pos;

    public List<string> ParseErrors { get; } = new();

    public RapidParser(List<RapidToken> tokens)
    {
        _tokens = tokens ?? new List<RapidToken>();
        _pos = 0;
    }

    private RapidToken Current => _pos < _tokens.Count ? _tokens[_pos] : new RapidToken(RapidTokenType.EOF, "", 0, 0);

    private RapidToken Advance()
    {
        var tok = Current;
        if (_pos < _tokens.Count) _pos++;
        return tok;
    }

    private bool Check(RapidTokenType type, string? kwValue = null)
    {
        if (Current.Type != type) return false;
        if (kwValue != null && !string.Equals(Current.Value, kwValue, StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    public BlockNode ParseBlock(params string[] stopKeywords)
    {
        var block = new BlockNode { Line = Current.Line, Column = Current.Column };

        while (Current.Type != RapidTokenType.EOF)
        {
            if (Current.Type == RapidTokenType.Keyword)
            {
                bool isStop = false;
                foreach (var kw in stopKeywords)
                {
                    if (string.Equals(Current.Value, kw, StringComparison.OrdinalIgnoreCase))
                    {
                        isStop = true;
                        break;
                    }
                }
                if (isStop) break;
            }

            var stmt = ParseStatement();
            if (stmt != null)
            {
                block.Statements.Add(stmt);
            }
        }

        return block;
    }

    private AstNode? ParseStatement()
    {
        if (Current.Type == RapidTokenType.EOF) return null;

        // Label definition check: identifier :
        if (Current.Type == RapidTokenType.Identifier && Peek(1).Type == RapidTokenType.Colon)
        {
            var lblTok = Advance();
            Advance(); // consume :
            return new LabelNode
            {
                Line = lblTok.Line,
                Column = lblTok.Column,
                LabelName = lblTok.Value
            };
        }

        if (Check(RapidTokenType.Keyword, "IF"))
        {
            return ParseIfStatement();
        }

        if (Check(RapidTokenType.Keyword, "WHILE"))
        {
            return ParseWhileStatement();
        }

        if (Check(RapidTokenType.Keyword, "FOR"))
        {
            return ParseForStatement();
        }

        if (Check(RapidTokenType.Keyword, "TEST"))
        {
            return ParseTestStatement();
        }

        if (Check(RapidTokenType.Keyword, "GOTO"))
        {
            var gotoTok = Advance(); // consume GOTO
            var labelName = "";
            if (Current.Type == RapidTokenType.Identifier || Current.Type == RapidTokenType.Keyword)
            {
                labelName = Advance().Value;
            }
            if (Check(RapidTokenType.Semicolon)) Advance();
            return new GotoNode
            {
                Line = gotoTok.Line,
                Column = gotoTok.Column,
                TargetLabel = labelName
            };
        }

        // Generic statement
        var startLine = Current.Line;
        var startCol = Current.Column;
        var textSb = new StringBuilder();
        var identifiers = new List<string>();

        while (Current.Type != RapidTokenType.EOF && Current.Type != RapidTokenType.Semicolon)
        {
            if (Current.Type == RapidTokenType.Keyword &&
                (string.Equals(Current.Value, "IF", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(Current.Value, "WHILE", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(Current.Value, "FOR", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(Current.Value, "TEST", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(Current.Value, "END_IF", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(Current.Value, "ENDIF", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(Current.Value, "ENDWHILE", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(Current.Value, "ENDFOR", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(Current.Value, "ENDTEST", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(Current.Value, "ELSIF", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(Current.Value, "ELSE", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(Current.Value, "CASE", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(Current.Value, "DEFAULT", StringComparison.OrdinalIgnoreCase)))
            {
                break;
            }

            var tok = Advance();
            textSb.Append(tok.Value).Append(' ');
            if (tok.Type == RapidTokenType.Identifier)
            {
                identifiers.Add(tok.Value);
            }
        }

        if (Check(RapidTokenType.Semicolon)) Advance();

        return new StatementNode
        {
            Line = startLine,
            Column = startCol,
            Text = textSb.ToString().Trim(),
            Identifiers = identifiers
        };
    }

    private IfNode ParseIfStatement()
    {
        var ifTok = Advance(); // consume IF
        var condSb = new StringBuilder();
        while (Current.Type != RapidTokenType.EOF && !Check(RapidTokenType.Keyword, "THEN"))
        {
            condSb.Append(Advance().Value).Append(' ');
        }
        if (Check(RapidTokenType.Keyword, "THEN")) Advance();

        var node = new IfNode
        {
            Line = ifTok.Line,
            Column = ifTok.Column,
            Condition = condSb.ToString().Trim()
        };

        node.ThenBlock = ParseBlock("ELSIF", "ELSE", "END_IF", "ENDIF");

        while (Check(RapidTokenType.Keyword, "ELSIF"))
        {
            Advance(); // consume ELSIF
            var elsifCondSb = new StringBuilder();
            while (Current.Type != RapidTokenType.EOF && !Check(RapidTokenType.Keyword, "THEN"))
            {
                elsifCondSb.Append(Advance().Value).Append(' ');
            }
            if (Check(RapidTokenType.Keyword, "THEN")) Advance();

            var elsifBlock = ParseBlock("ELSIF", "ELSE", "END_IF", "ENDIF");
            node.ElsifBranches.Add((elsifCondSb.ToString().Trim(), elsifBlock));
        }

        if (Check(RapidTokenType.Keyword, "ELSE"))
        {
            Advance(); // consume ELSE
            node.ElseBlock = ParseBlock("END_IF", "ENDIF");
        }

        if (Check(RapidTokenType.Keyword, "END_IF") || Check(RapidTokenType.Keyword, "ENDIF"))
        {
            Advance();
            node.HasEndIf = true;
            if (Check(RapidTokenType.Semicolon)) Advance();
        }
        else
        {
            node.HasEndIf = false;
            ParseErrors.Add($"IF statement at line {ifTok.Line} is missing closing END_IF.");
        }

        return node;
    }

    private WhileNode ParseWhileStatement()
    {
        var whileTok = Advance(); // consume WHILE
        var condSb = new StringBuilder();
        while (Current.Type != RapidTokenType.EOF && !Check(RapidTokenType.Keyword, "DO"))
        {
            condSb.Append(Advance().Value).Append(' ');
        }
        if (Check(RapidTokenType.Keyword, "DO")) Advance();

        var node = new WhileNode
        {
            Line = whileTok.Line,
            Column = whileTok.Column,
            Condition = condSb.ToString().Trim(),
            Body = ParseBlock("ENDWHILE")
        };

        if (Check(RapidTokenType.Keyword, "ENDWHILE"))
        {
            Advance();
            node.HasEndWhile = true;
            if (Check(RapidTokenType.Semicolon)) Advance();
        }
        else
        {
            node.HasEndWhile = false;
            ParseErrors.Add($"WHILE statement at line {whileTok.Line} is missing closing ENDWHILE.");
        }

        return node;
    }

    private ForNode ParseForStatement()
    {
        var forTok = Advance(); // consume FOR
        var varName = Current.Type == RapidTokenType.Identifier ? Advance().Value : "";
        if (Check(RapidTokenType.Keyword, "FROM")) Advance();
        var fromSb = new StringBuilder();
        while (Current.Type != RapidTokenType.EOF && !Check(RapidTokenType.Keyword, "TO"))
        {
            fromSb.Append(Advance().Value).Append(' ');
        }
        if (Check(RapidTokenType.Keyword, "TO")) Advance();
        var toSb = new StringBuilder();
        while (Current.Type != RapidTokenType.EOF && !Check(RapidTokenType.Keyword, "DO"))
        {
            toSb.Append(Advance().Value).Append(' ');
        }
        if (Check(RapidTokenType.Keyword, "DO")) Advance();

        var node = new ForNode
        {
            Line = forTok.Line,
            Column = forTok.Column,
            VarName = varName,
            FromExpr = fromSb.ToString().Trim(),
            ToExpr = toSb.ToString().Trim(),
            Body = ParseBlock("ENDFOR")
        };

        if (Check(RapidTokenType.Keyword, "ENDFOR"))
        {
            Advance();
            node.HasEndFor = true;
            if (Check(RapidTokenType.Semicolon)) Advance();
        }
        else
        {
            node.HasEndFor = false;
            ParseErrors.Add($"FOR statement at line {forTok.Line} is missing closing ENDFOR.");
        }

        return node;
    }

    private TestNode ParseTestStatement()
    {
        var testTok = Advance(); // consume TEST
        var exprSb = new StringBuilder();
        while (Current.Type != RapidTokenType.EOF && !Check(RapidTokenType.Keyword, "CASE") && !Check(RapidTokenType.Keyword, "DEFAULT") && !Check(RapidTokenType.Keyword, "ENDTEST"))
        {
            exprSb.Append(Advance().Value).Append(' ');
        }

        var node = new TestNode
        {
            Line = testTok.Line,
            Column = testTok.Column,
            Expression = exprSb.ToString().Trim()
        };

        while (Check(RapidTokenType.Keyword, "CASE"))
        {
            Advance(); // consume CASE
            var caseValSb = new StringBuilder();
            while (Current.Type != RapidTokenType.EOF && Current.Type != RapidTokenType.Colon)
            {
                caseValSb.Append(Advance().Value).Append(' ');
            }
            if (Check(RapidTokenType.Colon)) Advance();

            var caseBlock = ParseBlock("CASE", "DEFAULT", "ENDTEST");
            node.Cases.Add((caseValSb.ToString().Trim(), caseBlock));
        }

        if (Check(RapidTokenType.Keyword, "DEFAULT"))
        {
            Advance();
            if (Check(RapidTokenType.Colon)) Advance();
            node.DefaultBlock = ParseBlock("ENDTEST");
        }

        if (Check(RapidTokenType.Keyword, "ENDTEST"))
        {
            Advance();
            node.HasEndTest = true;
            if (Check(RapidTokenType.Semicolon)) Advance();
        }
        else
        {
            node.HasEndTest = false;
            ParseErrors.Add($"TEST statement at line {testTok.Line} is missing closing ENDTEST.");
        }

        return node;
    }

    private RapidToken Peek(int offset)
    {
        int idx = _pos + offset;
        return idx >= 0 && idx < _tokens.Count ? _tokens[idx] : new RapidToken(RapidTokenType.EOF, "", 0, 0);
    }
}
