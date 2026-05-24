using GrafcetStudio.CodeGen.Profile;
using GrafcetStudio.Domain.Models;
using System;

namespace GrafcetStudio.App.Generators;

public class ProfiledMnemonicGenerator : ICodeGenerator
{
    private readonly string _platform;
    private readonly KeyenceMnemonicGenerator _baseGenerator = new();

    public ProfiledMnemonicGenerator(string platform)
    {
        _platform = platform;
    }

    public string Platform => _platform;

    public string Generate(CodegenPayload payload)
    {
        var code = _baseGenerator.Generate(payload);
        var profile = ProfileRegistry.Get(_platform);
        return string.Equals(profile.Id, KeyenceMnemonicGenerator.DefaultPlatform, StringComparison.OrdinalIgnoreCase)
            ? code
            : ProfileRegistry.ApplyProfile(code, profile);
    }
}
