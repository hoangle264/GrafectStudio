using System;
using System.Linq;

namespace GrafcetStudio.App.Services.Siemens;

/// <summary>TIA Portal versions that have a GrafcetStudio.TiaBridge.V{n} executable.</summary>
public static class SiemensTiaVersions
{
    public const string Default = "V19";

    public static readonly string[] Supported = ["V19", "V20"];

    /// <summary>Accepts "V20", "v20" or "20"; returns the canonical "V20", or null when unsupported.</summary>
    public static string? Normalize(string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        var canonical = trimmed!.StartsWith("V", StringComparison.OrdinalIgnoreCase) ? "V" + trimmed.Substring(1) : "V" + trimmed;
        return Supported.FirstOrDefault(version => string.Equals(version, canonical, StringComparison.OrdinalIgnoreCase));
    }

    public static string NormalizeOrDefault(string? value) => Normalize(value) ?? Default;

    public static uint ToNumber(string version) => uint.Parse(NormalizeOrDefault(version).Substring(1));

    public static string BridgeExecutableName(string version) => $"GrafcetStudio.TiaBridge.{NormalizeOrDefault(version)}.exe";
}
