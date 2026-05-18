using System.Collections.Generic;

namespace GrafcetStudio.Domain.Models;

/// <summary>Represents an instantiated device variable and its signal address map.</summary>
public class DeviceVariable
{
    public string Label { get; init; } = string.Empty;

    public string Format { get; init; } = string.Empty;

    public string? Address { get; init; }

    public IDictionary<string, string> SignalAddresses { get; init; } = new Dictionary<string, string>();

    public string? GetSignalAddress(string signalId)
        => SignalAddresses.TryGetValue(signalId, out var address) ? address : null;

    public string? GetSignalAddressByName(string signalName, DeviceType deviceType)
    {
        var signal = deviceType.Signals.FirstOrDefault(s =>
            string.Equals(s.Name, signalName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(s.Id, signalName, StringComparison.OrdinalIgnoreCase));

        return signal is null ? null : GetSignalAddress(signal.Id) ?? GetSignalAddress(signal.Name);
    }
}
