namespace GrafcetStudio.Domain.Models;

/// <summary>Represents resolved physical signal information for generation.</summary>
public class SignalInfo
{
    public string PhysAddr { get; init; } = string.Empty;

    public string? DevLabel { get; init; }

    public string? SigName { get; init; }

    public string? DevTypeName { get; init; }
}
