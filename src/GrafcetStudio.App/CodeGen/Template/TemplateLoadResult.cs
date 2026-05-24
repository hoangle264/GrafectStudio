using System.Collections.Generic;

namespace GrafcetStudio.CodeGen.Template;

public class TemplateLoadResult
{
    public Dictionary<string, TemplateEntry> Templates { get; } = new(StringComparer.OrdinalIgnoreCase);

    public List<string> Errors { get; } = new();

    public List<string> Warnings { get; } = new();

    public bool IsValid => Errors.Count == 0;

    public int LoadedCount => Templates.Count;
}
