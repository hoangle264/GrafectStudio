using System.Collections.Generic;
using GrafcetStudio.Domain.Models;

namespace GrafcetStudio.CodeGen.Models;

/// <summary>Represents a fully prepared diagram unit for generation.</summary>
public class DiagramEntry
{
    public DiagramMeta Meta { get; init; } = new();

    public DiagramState State { get; init; } = new();

    public string Mode { get; init; } = string.Empty;

    public IList<SequenceEntry> Sequence { get; init; } = new List<SequenceEntry>();

    public IDictionary<string, MrPair> MrMap { get; init; } = new Dictionary<string, MrPair>();
}
