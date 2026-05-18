using GrafcetStudio.Domain.Enums;

namespace GrafcetStudio.Domain.Models;

/// <summary>Represents a resolved sequence row around a specific step.</summary>
public class SequenceEntry
{
    public Step Step { get; init; } = new();

    public Transition? InTransition { get; init; }

    public Transition? OutTransition { get; init; }

    public BranchType BranchType { get; init; }
}
