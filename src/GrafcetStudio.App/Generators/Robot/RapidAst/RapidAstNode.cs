using System.Collections.Generic;

namespace GrafcetStudio.App.Generators.Robot.RapidAst;

public abstract class AstNode
{
    public int Line { get; set; }
    public int Column { get; set; }
}

public class BlockNode : AstNode
{
    public List<AstNode> Statements { get; set; } = new();
}

public class IfNode : AstNode
{
    public string Condition { get; set; } = string.Empty;
    public BlockNode ThenBlock { get; set; } = new();
    public List<(string Condition, BlockNode Block)> ElsifBranches { get; set; } = new();
    public BlockNode? ElseBlock { get; set; }
    public bool HasEndIf { get; set; }
}

public class WhileNode : AstNode
{
    public string Condition { get; set; } = string.Empty;
    public BlockNode Body { get; set; } = new();
    public bool HasEndWhile { get; set; }
}

public class ForNode : AstNode
{
    public string VarName { get; set; } = string.Empty;
    public string FromExpr { get; set; } = string.Empty;
    public string ToExpr { get; set; } = string.Empty;
    public BlockNode Body { get; set; } = new();
    public bool HasEndFor { get; set; }
}

public class TestNode : AstNode
{
    public string Expression { get; set; } = string.Empty;
    public List<(string CaseExpr, BlockNode Block)> Cases { get; set; } = new();
    public BlockNode? DefaultBlock { get; set; }
    public bool HasEndTest { get; set; }
}

public class GotoNode : AstNode
{
    public string TargetLabel { get; set; } = string.Empty;
}

public class LabelNode : AstNode
{
    public string LabelName { get; set; } = string.Empty;
}

public class StatementNode : AstNode
{
    public string Text { get; set; } = string.Empty;
    public List<string> Identifiers { get; set; } = new();
}
