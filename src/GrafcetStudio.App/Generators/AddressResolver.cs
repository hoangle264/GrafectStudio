using GrafcetStudio.App.Models;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace GrafcetStudio.App.Generators;

public static class AddressResolver
{
    private static readonly Regex PlcAddressRegex = new("^[@%]|^[A-Z]{1,3}\\d", RegexOptions.Compiled);

    public static string Resolve(string varOrAddr, IReadOnlyList<Variable> variables)
    {
        if (string.IsNullOrWhiteSpace(varOrAddr)) return string.Empty;
        if (PlcAddressRegex.IsMatch(varOrAddr)) return varOrAddr;

        var exact = variables.FirstOrDefault(v => v.Label == varOrAddr);
        if (!string.IsNullOrWhiteSpace(exact?.Address)) return exact.Address!;

        var dotIndex = varOrAddr.IndexOf('.');
        if (dotIndex > 0 && dotIndex < varOrAddr.Length - 1)
        {
            var instanceLabel = varOrAddr[..dotIndex];
            var signalName = varOrAddr[(dotIndex + 1)..];
            var variable = variables.FirstOrDefault(v => v.Label == instanceLabel);
            if (variable?.SignalAddresses is not null
                && variable.SignalAddresses.TryGetValue(signalName, out var signalAddress)
                && !string.IsNullOrWhiteSpace(signalAddress))
            {
                return signalAddress;
            }
        }

        return varOrAddr;
    }
}
