using GrafcetStudio.Domain.Enums;

namespace GrafcetStudio.CodeGen.Models;

/// <summary>Represents a generation diagnostic entry.</summary>
public class Diagnostic
{
    public DiagnosticLevel Level { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;
}
