using GrafcetStudio.Domain.Enums;

namespace GrafcetStudio.Domain.Models;

/// <summary>Represents a signal definition available on a device type.</summary>
public class DeviceSignal
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string DataType { get; init; } = string.Empty;

    public SignalVarType VarType { get; init; }

    public string Comment { get; init; } = string.Empty;
}
