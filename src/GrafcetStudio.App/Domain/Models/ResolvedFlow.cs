using System.Collections.Generic;

namespace GrafcetStudio.Domain.Models;

/// <summary>Template-friendly flow with resolved step sequence.</summary>
public class ResolvedFlow
{
    public string? id { get; init; }

    public string? name { get; init; }

    public string? type { get; init; }

    public string? mode { get; init; }

    public string normalizedType { get; init; } = string.Empty;

    public DiagramInfo? diagram { get; init; }

    public IList<ResolvedStep> steps { get; init; } = new List<ResolvedStep>();

    public IList<ResolvedStep> resolvedSteps => steps;

    public IList<ResolvedStep> sequence => steps;

    public IList<Step> rawSteps { get; init; } = new List<Step>();

    public IList<Transition> transitions { get; init; } = new List<Transition>();
}
