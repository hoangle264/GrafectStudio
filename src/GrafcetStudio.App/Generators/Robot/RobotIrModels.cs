using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GrafcetStudio.App.Generators.Robot;

public class RobotIrDocument
{
    [JsonPropertyName("$schema")]
    public string Schema { get; set; } = "grafectstudio/robot-ir/v1";

    [JsonPropertyName("platform")]
    public string Platform { get; set; } = "ABB_RAPID";

    [JsonPropertyName("module")]
    public string Module { get; set; } = "MainModule";

    [JsonPropertyName("signals")]
    public List<RobotSignal> Signals { get; set; } = new();

    [JsonPropertyName("positions")]
    public List<RobotPosition> Positions { get; set; } = new();

    [JsonPropertyName("tools")]
    public List<RobotTool> Tools { get; set; } = new();

    [JsonPropertyName("init")]
    public RobotInitBlock Init { get; set; } = new();

    [JsonPropertyName("steps")]
    public List<RobotStep> Steps { get; set; } = new();

    [JsonPropertyName("flow")]
    public RobotFlowInfo Flow { get; set; } = new();
}

public class RobotSignal
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty; // DO, DI, AO, AI

    [JsonPropertyName("alias")]
    public string Alias { get; set; } = string.Empty;
}

public class RobotPosition
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("motionType")]
    public string MotionType { get; set; } = "Joint"; // Joint, Linear, AbsJ

    [JsonPropertyName("speed")]
    public string Speed { get; set; } = "Medium"; // Fast, Medium, Precise

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

public class RobotTool
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "tool1";

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

public class RobotInitBlock
{
    [JsonPropertyName("instructions")]
    public List<RobotInstruction> Instructions { get; set; } = new();
}

public class RobotStep
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("grafectStepId")]
    public int GrafectStepId { get; set; }

    [JsonPropertyName("instructions")]
    public List<RobotInstruction> Instructions { get; set; } = new();
}

public class RobotInstruction
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("target")]
    public string? Target { get; set; }

    [JsonPropertyName("tool")]
    public string? Tool { get; set; }

    [JsonPropertyName("signal")]
    public string? Signal { get; set; }

    [JsonPropertyName("value")]
    public object? Value { get; set; }

    [JsonPropertyName("seconds")]
    public double? Seconds { get; set; }

    [JsonPropertyName("offset")]
    public RobotOffset? Offset { get; set; }
}

public class RobotOffset
{
    [JsonPropertyName("x")]
    public object? X { get; set; }

    [JsonPropertyName("y")]
    public object? Y { get; set; }

    [JsonPropertyName("z")]
    public object? Z { get; set; }
}

public class RobotFlowInfo
{
    [JsonPropertyName("initFirst")]
    public bool InitFirst { get; set; } = true;

    [JsonPropertyName("mainLoopSteps")]
    public List<int> MainLoopSteps { get; set; } = new();

    [JsonPropertyName("loopBack")]
    public bool LoopBack { get; set; } = true;
}
