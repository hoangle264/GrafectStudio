using SimaticML.API;

namespace GrafcetStudio.App.Generators.Siemens;

/// <summary>
/// SimaticMLAPI.TIA_VERSION is a process-wide static, so every Siemens generator must set it
/// before building a document instead of inheriting whatever the previous generator left behind.
/// </summary>
internal static class SiemensTiaXmlVersion
{
    // Same fallback as TIA_VERSION in the bundled templates/siemens-lad/*.lad.hbs.
    public const uint Default = 17;

    /// <summary>Push target version wins, then the template's TIA_VERSION, then <see cref="Default"/>.</summary>
    public static void Apply(uint pushTiaVersion, uint templateTiaVersion = 0)
        => SimaticMLAPI.TIA_VERSION = pushTiaVersion > 0 ? pushTiaVersion
            : templateTiaVersion > 0 ? templateTiaVersion
            : Default;
}
