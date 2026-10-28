using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Linq;
namespace GrafcetStudio.Domain.Models;

/// <summary>Represents an instantiated device variable and its signal address map.</summary>
public class DeviceVariable
{
    [JsonPropertyName("label")]
    public string Label { get; init; } = string.Empty;

    [JsonPropertyName("format")]
    public string Format { get; init; } = string.Empty;

    [JsonPropertyName("address")]
    public string? Address { get; set; }

    [JsonPropertyName("signalAddresses")]
    public IDictionary<string, string> SignalAddresses { get; set; } = new Dictionary<string, string>();

    [JsonPropertyName("declarationMode")]
    public string? DeclarationMode { get; init; }

    [JsonPropertyName("blockId")]
    public string BlockId { get; init; } = string.Empty;

    public string? GetSignalAddress(string signalId)
        => SignalAddresses.TryGetValue(signalId, out var address) ? address : null;

    /// <summary>
    /// Returns the signal's PLC address, or the label <c>{Label}.{signal}</c> when no address is assigned.
    /// </summary>
    public string GetSignalAddressOrLabel(string signalId)
    {
        var address = GetSignalAddress(signalId);
        return string.IsNullOrWhiteSpace(address) ? $"{Label}.{signalId}" : address!;
    }

    /// <summary>Returns the variable's PLC address, or its label when no address is assigned.</summary>
    public string AddressOrLabel => string.IsNullOrWhiteSpace(Address) ? Label : Address!;

    public string? GetSignalAddressByName(string signalName, DeviceType deviceType)
    {
        var signal = deviceType.Signals.FirstOrDefault(s =>
            string.Equals(s.Name, signalName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(s.Id, signalName, StringComparison.OrdinalIgnoreCase));

        return signal is null ? null : GetSignalAddress(signal.Id) ?? GetSignalAddress(signal.Name);
    }
}
