using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace GrafcetStudio.Domain.Resolution;

/// <summary>
/// Describes which strings are PLC address literals for one vendor. A value is an address only when
/// it fully matches one of the vendor's patterns; anything else is treated as a label.
/// </summary>
public sealed class PlcAddressFormat
{
    private readonly Regex[] _patterns;

    public PlcAddressFormat(string vendor, IEnumerable<string> devicePrefixes, IEnumerable<string> patterns)
    {
        Vendor = vendor;
        DevicePrefixes = devicePrefixes.ToList();
        _patterns = patterns
            .Select(pattern => new Regex($"^(?:{pattern})$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            .ToArray();
    }

    public string Vendor { get; }

    /// <summary>Device codes accepted by this vendor (for display/documentation).</summary>
    public IReadOnlyList<string> DevicePrefixes { get; }

    public bool IsAddress(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var trimmed = value.Trim();
        return _patterns.Any(pattern => pattern.IsMatch(trimmed));
    }
}

/// <summary>Per-vendor address lists. Add a vendor here instead of widening a shared regex.</summary>
public static class PlcAddressFormats
{
    // KV-8000/7000/5000 standard device notation. "@" marks a local (program-scoped) device.
    private static readonly string[] KeyenceDevices =
    {
        "CTH", "CTC",
        "MR", "LR", "CR", "VB", "DM", "EM", "FM", "ZF", "TM", "CM", "VM",
        "R", "B", "W", "Z", "T", "C"
    };

    public static readonly PlcAddressFormat Keyence = new(
        "Keyence",
        KeyenceDevices,
        new[]
        {
            // R1000, @MR300, DM100, DM100.5 (bit of a word device, 0-F)
            $@"@?(?:{string.Join("|", KeyenceDevices)})\d+(?:\.[0-9A-F]{{1,2}})?"
        });

    private static readonly string[] SiemensAreas = { "I", "Q", "M", "DB" };

    public static readonly PlcAddressFormat Siemens = new(
        "Siemens",
        SiemensAreas,
        new[]
        {
            // %I0.0, %IX0.0, %QB1, %MW10, %MD20 (the leading % is optional)
            @"%?[IQM](?:X?\d+\.[0-7]|[BWD]\d+)",
            // %DB1.DBX0.0, %DB1.DBW2
            @"%?DB\d+\.DB(?:X\d+\.[0-7]|[BWD]\d+)",
            // "Tag".%X3, Db.Member.%X0 (bit slice of a symbolic tag)
            @"#?(?:""[^""]+""|[A-Za-z_][A-Za-z0-9_]*)(?:\.(?:""[^""]+""|[A-Za-z_][A-Za-z0-9_]*))*\.%X\d+"
        });

    public static IReadOnlyList<PlcAddressFormat> All { get; } = new[] { Keyence, Siemens };
}
