using System.Collections.Generic;
using System.Text.RegularExpressions;
using GrafcetStudio.Domain.Models;

namespace GrafcetStudio.Domain.Resolution;

/// <summary>Provides static helpers to resolve variables, signals, and PLC literal addresses.</summary>
public static class SignalResolver
{
    private static readonly Regex PlcAddressRegex = new("^(@MR\\d+|%[IQM][A-Z]?\\d|[A-Z]{1,3}\\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string? ResolveAddress(string varOrAddr, IList<DeviceVariable> vars)
        => ResolveSignalInfo(varOrAddr, vars)?.PhysAddr;

    public static SignalInfo? ResolveSignalInfo(string varOrAddr, IList<DeviceVariable> vars)
    {
        if (string.IsNullOrWhiteSpace(varOrAddr)) return null;
        if (IsPlcAddressLiteral(varOrAddr)) return new SignalInfo { PhysAddr = varOrAddr };

        var dotIndex = varOrAddr.IndexOf('.');
        if (dotIndex > 0 && dotIndex < varOrAddr.Length - 1)
        {
            var label = varOrAddr[..dotIndex];
            var signal = varOrAddr[(dotIndex + 1)..];
            var device = vars.FirstOrDefault(v => string.Equals(v.Label, label, StringComparison.OrdinalIgnoreCase));
            if (device is not null)
            {
                var address = device.GetSignalAddress(signal);
                if (!string.IsNullOrWhiteSpace(address))
                {
                    return new SignalInfo { PhysAddr = address!, DevLabel = device.Label, SigName = signal, DevTypeName = device.Format };
                }
            }
        }

        var variable = vars.FirstOrDefault(v => string.Equals(v.Label, varOrAddr, StringComparison.OrdinalIgnoreCase));
        return string.IsNullOrWhiteSpace(variable?.Address)
            ? null
            : new SignalInfo { PhysAddr = variable.Address!, DevLabel = variable.Label, DevTypeName = variable.Format };
    }

    public static SignalInfo? FindDeviceByAddress(string physAddr, IList<DeviceVariable> vars)
    {
        foreach (var variable in vars)
        {
            if (string.Equals(variable.Address, physAddr, StringComparison.OrdinalIgnoreCase))
            {
                return new SignalInfo { PhysAddr = physAddr, DevLabel = variable.Label, DevTypeName = variable.Format };
            }

            var signal = variable.SignalAddresses.FirstOrDefault(p => string.Equals(p.Value, physAddr, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(signal.Key))
            {
                return new SignalInfo { PhysAddr = physAddr, DevLabel = variable.Label, SigName = signal.Key, DevTypeName = variable.Format };
            }
        }

        return null;
    }

    public static string? FindModeBit(IList<DeviceVariable> vars)
        => vars.FirstOrDefault(v => v.Label.Contains("mode", StringComparison.OrdinalIgnoreCase))?.Address;

    public static string? FindErrorBit(IList<DeviceVariable> vars)
        => vars.FirstOrDefault(v => v.Label.Contains("error", StringComparison.OrdinalIgnoreCase))?.Address;

    public static bool IsPlcAddressLiteral(string value)
        => !string.IsNullOrWhiteSpace(value) && PlcAddressRegex.IsMatch(value.Trim());
}
