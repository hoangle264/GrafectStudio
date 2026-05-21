using System.Collections.Generic;
using System.Linq;
using GrafcetStudio.Domain.Models;

namespace GrafcetStudio.App.Generators;

public class SequenceItem
{
    public required Step Step { get; init; }
    public Transition? InTransition { get; init; }
    public Transition? OutTransition { get; init; }
}

public static class SequenceBuilder
{
    public static IReadOnlyList<SequenceItem> Build(CodegenPayload payload)
        => Build(payload.ToDiagramState());

    public static IReadOnlyList<SequenceItem> Build(DiagramState state)
    {
        var steps = state.Steps.OrderBy(s => s.Number).ToList();
        if (steps.Count == 0) return new List<SequenceItem>();

        var byId = steps.ToDictionary(s => s.Id, s => s);
        var transitions = state.Transitions;
        var current = steps.FirstOrDefault(s => s.IsInitial) ?? steps[0];
        var visited = new HashSet<string>();
        var ordered = new List<Step>();

        while (current is not null && visited.Add(current.Id))
        {
            ordered.Add(current);
            var outTransitionId = state.Connections.FirstOrDefault(c => c.From == current.Id)?.To;
            var nextId = outTransitionId is null
                ? null
                : state.Connections.FirstOrDefault(c => c.From == outTransitionId)?.To;
            current = nextId is not null && byId.TryGetValue(nextId, out var next) ? next : null;
        }

        if (ordered.Count != steps.Count) ordered = steps;

        return ordered.Select(s => new SequenceItem
        {
            Step = s,
            InTransition = ResolveTransition(state.Connections.FirstOrDefault(c => c.To == s.Id)?.From, transitions),
            OutTransition = ResolveTransition(state.Connections.FirstOrDefault(c => c.From == s.Id)?.To, transitions)
        }).ToList();
    }

    private static Transition? ResolveTransition(string? transitionId, IList<Transition> transitions)
        => transitionId is null ? null : transitions.FirstOrDefault(t => t.Id == transitionId);
}
