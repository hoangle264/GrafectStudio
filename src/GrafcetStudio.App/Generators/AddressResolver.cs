using System.Collections.Generic;
using System.Linq;
using GrafcetStudio.Domain.Models;
using GrafcetStudio.Domain.Resolution;

namespace GrafcetStudio.App.Generators;

public static class AddressResolver
{
    public static string Resolve(string varOrAddr, IReadOnlyList<DeviceVariable> variables)
    {
        if (string.IsNullOrWhiteSpace(varOrAddr)) return string.Empty;
        return SignalResolver.ResolveAddress(varOrAddr, variables.ToList()) ?? varOrAddr;
    }
}
