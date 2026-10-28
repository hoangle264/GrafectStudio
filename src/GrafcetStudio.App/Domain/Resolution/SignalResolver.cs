using System.Collections.Generic;
using GrafcetStudio.Domain.Models;

namespace GrafcetStudio.Domain.Resolution;

/// <summary>Provides static helpers to resolve variables, signals, and PLC literal addresses.</summary>
public static class SignalResolver
{
    public static string? ResolveAddress(string varOrAddr, IList<DeviceVariable> vars, PlcAddressFormat format)
        => ResolveSignalInfo(varOrAddr, vars, format)?.PhysAddr;

    public static SignalInfo? ResolveSignalInfo(string varOrAddr, IList<DeviceVariable> vars, PlcAddressFormat format)
    {
        if (string.IsNullOrWhiteSpace(varOrAddr)) return null;
        if (format.IsAddress(varOrAddr)) return new SignalInfo { PhysAddr = varOrAddr };

        var dotIndex = varOrAddr.IndexOf('.');
        if (dotIndex > 0 && dotIndex < varOrAddr.Length - 1)
        {
            var label = varOrAddr[..dotIndex];
            var signal = varOrAddr[(dotIndex + 1)..];
            var device = vars.FirstOrDefault(v => string.Equals(v.Label, label, StringComparison.OrdinalIgnoreCase));
            if (device is not null)
            {
                return new SignalInfo { PhysAddr = device.GetSignalAddressOrLabel(signal), DevLabel = device.Label, SigName = signal, DevTypeName = device.Format };
            }
        }

        var variable = vars.FirstOrDefault(v => string.Equals(v.Label, varOrAddr, StringComparison.OrdinalIgnoreCase));
        return variable is null
            ? null
            : new SignalInfo { PhysAddr = variable.AddressOrLabel, DevLabel = variable.Label, DevTypeName = variable.Format };
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
}
