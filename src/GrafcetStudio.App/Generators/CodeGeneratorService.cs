using GrafcetStudio.App.Models;
using System;
using System.Collections.Generic;

namespace GrafcetStudio.App.Generators;

public class CodeGeneratorService : ICodeGeneratorService
{
    private readonly Dictionary<string, ICodeGenerator> _generators;

    public CodeGeneratorService(IEnumerable<ICodeGenerator> generators)
    {
        _generators = new(StringComparer.OrdinalIgnoreCase);
        foreach (var generator in generators) _generators[generator.Platform] = generator;
    }

    public string Generate(string platform, CodegenPayload data)
    {
        if (!_generators.TryGetValue(platform, out var generator))
            throw new InvalidOperationException($"Unsupported platform: {platform}");
        return generator.Generate(data);
    }
}
