using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace GrafcetStudio.CodeGen.Runtime.Models;

public class DeviceCommandComplete
{
    [JsonPropertyName("sensor")]
    public string Sensor { get; init; } = string.Empty;

    [JsonPropertyName("sensorLabel")]
    public string SensorLabel { get; init; } = string.Empty;

    [JsonPropertyName("value")]
    public string? Value { get; init; }
}

public class DeviceCommand
{
    [JsonPropertyName("actionLabel")]
    public string ActionLabel { get; init; } = string.Empty;

    [JsonPropertyName("driveSignal")]
    public string DriveSignal { get; init; } = string.Empty;

    [JsonPropertyName("complete")]
    public DeviceCommandComplete? Complete { get; init; }
}

public class DeviceLibraryEntry
{
    [JsonPropertyName("deviceId")]
    public string DeviceId { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("commands")]
    public IDictionary<string, DeviceCommand> Commands { get; init; } = new Dictionary<string, DeviceCommand>(StringComparer.OrdinalIgnoreCase);
}

public class DeviceLibraryRoot
{
    [JsonPropertyName("devices")]
    public IList<DeviceLibraryEntry> Devices { get; init; } = new List<DeviceLibraryEntry>();

    [JsonIgnore]
    public IDictionary<string, DeviceLibraryEntry> ById { get; }

    public DeviceLibraryRoot()
    {
        ById = new Dictionary<string, DeviceLibraryEntry>(StringComparer.OrdinalIgnoreCase);
    }

    [JsonConstructor]
    public DeviceLibraryRoot(IList<DeviceLibraryEntry>? devices)
    {
        Devices = devices ?? new List<DeviceLibraryEntry>();
        ById = Devices
            .Where(device => !string.IsNullOrWhiteSpace(device.DeviceId))
            .GroupBy(device => device.DeviceId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
    }
}
