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

    public string diagramType { get; init; } = "Macro";

    public DiagramInfo? diagram { get; init; }

    public string stepMinAddress { get; init; } = string.Empty;

    public string stepMaxAddress { get; init; } = string.Empty;

    public string sequenceEnd { get; init; } = string.Empty;

    public IList<ResolvedStep> steps { get; init; } = new List<ResolvedStep>();

    public IList<ResolvedStep> resolvedSteps => steps;

    public IList<ResolvedStep> sequence => steps;

    public IList<Step> rawSteps { get; init; } = new List<Step>();

    public IList<Transition> transitions { get; init; } = new List<Transition>();

    public IList<MacroBindingContext> macroBindings { get; init; } = new List<MacroBindingContext>();

    public IList<MacroBindingContext> callerMacroBindings { get; init; } = new List<MacroBindingContext>();

    public IList<MacroBindingContext> calleeMacroBindings { get; init; } = new List<MacroBindingContext>();
}


