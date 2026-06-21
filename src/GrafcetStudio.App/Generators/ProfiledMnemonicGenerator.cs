using GrafcetStudio.CodeGen.Profile;
using GrafcetStudio.Domain.Models;
using System;
using System.Linq;

namespace GrafcetStudio.App.Generators;

public class ProfiledMnemonicGenerator : LegacyCodeGeneratorBase
{
    private readonly string _platform;
    private readonly KeyenceMnemonicGenerator _baseGenerator = new();

    public ProfiledMnemonicGenerator(string platform)
    {
        _platform = platform;
    }

    public override string Platform => _platform;

    protected override string GenerateLegacy(CodegenPayload payload)
    {
        var code = _baseGenerator.GenerateFiles(payload).First().Content;
        var profile = ProfileRegistry.Get(_platform);
        return string.Equals(profile.Id, KeyenceMnemonicGenerator.DefaultPlatform, StringComparison.OrdinalIgnoreCase)
            ? code
            : ProfileRegistry.ApplyProfile(code, profile);
    }
}
