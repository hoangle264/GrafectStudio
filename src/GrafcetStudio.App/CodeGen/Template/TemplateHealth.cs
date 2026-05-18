using System.Collections.Generic;

namespace GrafcetStudio.CodeGen.Template;

/// <summary>Represents aggregate template health status.</summary>
public class TemplateHealth
{
    public bool IsValid => Errors.Count == 0;

    public IList<string> Errors { get; init; } = new List<string>();

    public IList<TemplateHealthEntry> Entries { get; init; } = new List<TemplateHealthEntry>();
}
