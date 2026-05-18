using System.Collections.Generic;

namespace GrafcetStudio.Domain.Models;

/// <summary>Represents a device type with its available signals.</summary>
public class DeviceType
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public IList<DeviceSignal> Signals { get; init; } = new List<DeviceSignal>();
}
