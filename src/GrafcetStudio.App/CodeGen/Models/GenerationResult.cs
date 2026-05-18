using System.Collections.Generic;

namespace GrafcetStudio.CodeGen.Models;

/// <summary>Represents aggregate generation output and diagnostics.</summary>
public class GenerationResult
{
    public string Code { get; init; } = string.Empty;

    public string Stats { get; init; } = string.Empty;

    public IList<Diagnostic> Diagnostics { get; init; } = new List<Diagnostic>();
}
