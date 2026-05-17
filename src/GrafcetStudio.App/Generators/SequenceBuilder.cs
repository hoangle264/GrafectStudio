using GrafcetStudio.App.Models;
using System.Collections.Generic;
using System.Linq;

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
    {
        var steps = payload.Steps.OrderBy(s => s.Number).ToList();
        if (steps.Count == 0) return new List<SequenceItem>();

        var byId = steps.ToDictionary(s => s.Id, s => s);
        var transitions = payload.Transitions;
        var current = steps.FirstOrDefault(s => s.Initial) ?? steps[0];
        var visited = new HashSet<string>();
        var ordered = new List<Step>();

        while (current is not null && visited.Add(current.Id))
        {
            ordered.Add(current);
            var outT = transitions.FirstOrDefault(t => t.FromStepIds.Contains(current.Id));
            var nextId = outT?.ToStepIds.FirstOrDefault();
            current = nextId is not null && byId.TryGetValue(nextId, out var next) ? next : null;
        }

        if (ordered.Count != steps.Count) ordered = steps;

        return ordered.Select(s => new SequenceItem
        {
            Step = s,
            InTransition = transitions.FirstOrDefault(t => t.ToStepIds.Contains(s.Id)),
            OutTransition = transitions.FirstOrDefault(t => t.FromStepIds.Contains(s.Id))
        }).ToList();
    }
}
