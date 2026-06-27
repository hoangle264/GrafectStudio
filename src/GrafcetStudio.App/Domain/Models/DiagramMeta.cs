namespace GrafcetStudio.Domain.Models;

/// <summary>Represents diagram metadata used in generation context.</summary>
public class DiagramMeta
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Mode { get; init; } = string.Empty;

    public string? ControlState { get; init; }

    public string Category { get; init; } = "normal";

    public OrchestratorConfig? OrchestratorConfig { get; init; }

    public string UnitId { get; init; } = string.Empty;

    public string Unit { get; init; } = string.Empty;

    public string DiagramType { get; init; } = "Macro";

    public string AddressMode { get; init; } = "bool";

    public string BoolAddressMode { get; init; } = "linear";

    public int? BaseMr { get; init; }

    public string ActiveWord { get; init; } = string.Empty;

    public string CompleteWord { get; init; } = string.Empty;
}
