using System.Collections.Generic;
using GrafcetStudio.CodeGen.Profile;
using GrafcetStudio.Domain.Models;

namespace GrafcetStudio.CodeGen.Models;

/// <summary>Defines generation options for code output.</summary>
public class GenerationOptions
{
    public int BaseMr { get; init; }

    public PlcProfile Profile { get; init; } = new();

    public bool SeparateOutputs { get; init; }

    public IDictionary<string, MrPair>? MrMap { get; init; }
}
