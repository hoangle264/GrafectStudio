namespace GrafcetStudio.Domain.Models;

/// <summary>Represents diagram metadata used in generation context.</summary>
public class DiagramMeta
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Mode { get; init; } = string.Empty;

    public string UnitId { get; init; } = string.Empty;
}
