namespace GrafcetStudio.Domain.Models;

/// <summary>Represents a directed link between two diagram nodes.</summary>
public class Connection
{
    public string From { get; init; } = string.Empty;

    public string To { get; init; } = string.Empty;

    public string? ParallelGroupId { get; init; }
}
