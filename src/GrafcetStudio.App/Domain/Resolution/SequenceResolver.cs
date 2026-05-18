using System.Collections.Generic;
using GrafcetStudio.Domain.Models;
using GrafcetStudio.Domain.Enums;

namespace GrafcetStudio.Domain.Resolution;

/// <summary>Resolves step sequences, transition neighborhoods, and MR address mapping.</summary>
public class SequenceResolver : ISequenceResolver
{
    public IList<SequenceEntry> Resolve(DiagramState state)
    {
        if (state.Steps.Count == 0) return new List<SequenceEntry>();

        var ordered = new List<Step>();
        var byId = state.Steps.ToDictionary(s => s.Id);
        var current = state.Steps.FirstOrDefault(s => s.IsInitial) ?? state.Steps.OrderBy(s => s.Number).First();
        var visited = new HashSet<string>();

        while (visited.Add(current.Id))
        {
            ordered.Add(current);
            var transitionId = state.Connections.FirstOrDefault(c => c.From == current.Id)?.To;
            if (transitionId is null) break;

            var nextStepId = state.Connections.FirstOrDefault(c => c.From == transitionId)?.To;
            if (nextStepId is null || !byId.TryGetValue(nextStepId, out current!)) break;
        }

        foreach (var step in state.Steps.OrderBy(s => s.Number))
        {
            if (!visited.Contains(step.Id)) ordered.Add(step);
        }

        return ordered.Select(step => new SequenceEntry
        {
            Step = step,
            InTransition = ResolveTransition(state.Connections.FirstOrDefault(c => c.To == step.Id)?.From, state),
            OutTransition = ResolveTransition(state.Connections.FirstOrDefault(c => c.From == step.Id)?.To, state),
            BranchType = ResolveBranchType(step, state)
        }).ToList();
    }

    public IList<Step> ResolveUpstream(string transitionId, DiagramState state)
        => ResolveNeighborSteps(transitionId, state, upstream: true);

    public IList<Step> ResolveDownstream(string transitionId, DiagramState state)
        => ResolveNeighborSteps(transitionId, state, upstream: false);

    public IDictionary<string, MrPair> AllocateMrMap(IList<SequenceEntry> entries, int baseMr)
    {
        var result = new Dictionary<string, MrPair>();
        for (var i = 0; i < entries.Count; i++)
        {
            result[entries[i].Step.Id] = new MrPair(FormatMrAddress(baseMr + i * 2), FormatMrAddress(baseMr + i * 2 + 1));
        }

        return result;
    }

    public string FormatMrAddress(int number) => $"@MR{number}";

    private static Transition? ResolveTransition(string? id, DiagramState state)
        => id is null ? null : state.Transitions.FirstOrDefault(t => t.Id == id);

    private static BranchType ResolveBranchType(Step step, DiagramState state)
    {
        var outCount = state.Connections.Count(c => c.From == step.Id);
        var inCount = state.Connections.Count(c => c.To == step.Id);
        if (outCount > 1) return BranchType.ParallelSplit;
        if (inCount > 1) return BranchType.ParallelJoin;
        return BranchType.Normal;
    }

    private static IList<Step> ResolveNeighborSteps(string transitionId, DiagramState state, bool upstream)
    {
        var stepIds = state.Connections
            .Where(c => upstream ? c.To == transitionId : c.From == transitionId)
            .Select(c => upstream ? c.From : c.To)
            .ToHashSet();

        return state.Steps.Where(s => stepIds.Contains(s.Id)).OrderBy(s => s.Number).ToList();
    }
}
