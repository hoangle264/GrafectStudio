namespace GrafcetStudio.App.Expressions;

public static class LogicExpressionUtilities
{
    public static LogicExpression CreateTag(string tag, bool negated = false)
        => new()
        {
            Type = "TAG",
            Ref = tag,
            Negated = negated
        };

    public static LogicExpression CreateLogical(string nodeType, IEnumerable<LogicExpression> nodes)
        => new()
        {
            Type = nodeType.Trim().ToUpperInvariant(),
            Nodes = nodes.ToList()
        };

    public static LogicExpression CreateNot(LogicExpression node)
        => new()
        {
            Type = "NOT",
            Node = node
        };

    public static LogicExpression Normalize(this LogicExpression expression)
    {
        var type = expression.Type?.Trim().ToUpperInvariant();
        return type switch
        {
            "TAG" => CreateTag(expression.Ref ?? string.Empty, expression.Negated),
            "AND" => NormalizeLogical("AND", expression.Nodes),
            "OR" => NormalizeLogical("OR", expression.Nodes),
            "NOT" => NormalizeNot(expression.Node),
            _ => expression
        };
    }

    public static void CollectRefs(LogicExpression? expression, ISet<string> refs)
    {
        if (expression is null) return;
        if (string.Equals(expression.Type, "TAG", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(expression.Ref)) refs.Add(expression.Ref);
            return;
        }

        if (expression.Node is not null) CollectRefs(expression.Node, refs);
        foreach (var child in expression.Nodes) CollectRefs(child, refs);
    }

    private static LogicExpression NormalizeNot(LogicExpression? node)
    {
        var normalizedNode = node?.Normalize();
        if (normalizedNode is null)
        {
            return new LogicExpression { Type = "NOT" };
        }

        if (string.Equals(normalizedNode.Type, "NOT", StringComparison.OrdinalIgnoreCase) && normalizedNode.Node is not null)
        {
            return normalizedNode.Node.Normalize();
        }

        if (string.Equals(normalizedNode.Type, "TAG", StringComparison.OrdinalIgnoreCase))
        {
            return CreateTag(normalizedNode.Ref ?? string.Empty, !normalizedNode.Negated);
        }

        return CreateNot(normalizedNode);
    }

    private static LogicExpression NormalizeLogical(string nodeType, IEnumerable<LogicExpression> nodes)
    {
        var normalizedChildren = nodes.Select(node => node.Normalize()).ToList();
        var flattened = new List<LogicExpression>();
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
}
