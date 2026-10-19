using System.Text.Json.Serialization;

namespace GrafcetStudio.App.Expressions;

public sealed class LogicExpression
{
    [JsonPropertyName("type")] public string Type { get; set; } = "TAG";
    [JsonPropertyName("ref")] public string? Ref { get; set; }
    [JsonPropertyName("negated")] public bool Negated { get; set; }
    [JsonPropertyName("qualifier")] public string? Qualifier { get; set; }
    [JsonPropertyName("operand1")] public string? Operand1 { get; set; }
    [JsonPropertyName("operand2")] public string? Operand2 { get; set; }
    [JsonPropertyName("compareOp")] public string? CompareOp { get; set; }
    [JsonPropertyName("node")] public LogicExpression? Node { get; set; }
    [JsonPropertyName("nodes")] public List<LogicExpression> Nodes { get; set; } = [];
}
