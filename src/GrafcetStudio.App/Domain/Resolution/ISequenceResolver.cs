using System.Collections.Generic;
using GrafcetStudio.Domain.Models;

namespace GrafcetStudio.Domain.Resolution;

/// <summary>Defines sequence resolution operations for diagram traversal and MR allocation.</summary>
public interface ISequenceResolver
{
    IList<SequenceEntry> Resolve(DiagramState state);

    IList<Step> ResolveUpstream(string transitionId, DiagramState state);

    IList<Step> ResolveDownstream(string transitionId, DiagramState state);

    IDictionary<string, MrPair> AllocateMrMap(IList<SequenceEntry> entries, int baseMr);

    string FormatMrAddress(int number);
}
