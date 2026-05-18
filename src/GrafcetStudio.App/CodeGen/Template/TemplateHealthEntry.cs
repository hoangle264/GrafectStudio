using GrafcetStudio.Domain.Enums;

namespace GrafcetStudio.CodeGen.Template;

/// <summary>Represents one template health check result.</summary>
public class TemplateHealthEntry
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public DiagnosticLevel Level { get; init; }

    public string Message { get; init; } = string.Empty;

    public bool Required { get; init; }

    public bool HasSource { get; init; }
}
