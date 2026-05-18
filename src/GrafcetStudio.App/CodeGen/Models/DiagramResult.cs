using System.Collections.Generic;

namespace GrafcetStudio.CodeGen.Models;

/// <summary>Represents generated lines and basic diagram statistics.</summary>
public class DiagramResult
{
    public IList<string> Lines { get; init; } = new List<string>();

    public int StepCount { get; init; }
}
