namespace GrafcetStudio.CodeGen.Models;

/// <summary>Represents resolved signal action data for output generation.</summary>
public class SignalActionEntry
{
    public string ExecMr { get; init; } = string.Empty;

    public string Mode { get; init; } = string.Empty;

    public int StepNumber { get; init; }

    public string StepLabel { get; init; } = string.Empty;

    public string PhysAddr { get; init; } = string.Empty;

    public string? DevLabel { get; init; }

    public string? SigName { get; init; }
}
