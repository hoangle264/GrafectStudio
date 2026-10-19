using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GrafcetStudio.App.Generators.Robot;

public record SnippetMapDocument
{
    [JsonPropertyName("schema")]
    public string Schema { get; init; } = "grafectstudio/robot-snippet-map/v1";

    [JsonPropertyName("platform")]
    public string Platform { get; init; } = "ABB_RAPID";

    [JsonPropertyName("module")]
    public string Module { get; init; } = "main";

    [JsonPropertyName("signals")]
    public List<SnippetSignal> Signals { get; init; } = new();

    [JsonPropertyName("positions")]
    public List<SnippetPosition> Positions { get; init; } = new();

    [JsonPropertyName("tools")]
    public List<SnippetTool> Tools { get; init; } = new();

    [JsonPropertyName("init")]
    public string? Init { get; init; }

    [JsonPropertyName("steps")]
    public Dictionary<int, string> Steps { get; init; } = new();

    [JsonPropertyName("labels")]
    public string? Labels { get; init; }
}

public record SnippetSignal(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("alias")] string Alias
);

public record SnippetPosition(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("motionType")] string MotionType,
    [property: JsonPropertyName("speed")] string Speed,
    [property: JsonPropertyName("description")] string? Description = null
);

public record SnippetTool(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string? Description = null
);
