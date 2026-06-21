using GrafcetStudio.Domain.Models;
using GrafcetStudio.CodeGen.Template;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GrafcetStudio.App.Generators;

public class CodeGeneratorService : ICodeGeneratorService
{
    private readonly Dictionary<string, ICodeGenerator> _generators;
    private readonly TemplateManager _templates;

    public CodeGeneratorService(IEnumerable<ICodeGenerator> generators, TemplateManager templates)
    {
        _generators = new(StringComparer.OrdinalIgnoreCase);
        foreach (var generator in generators) _generators[generator.Platform] = generator;
        _templates = templates;
    }

    public CodegenOutput Generate(string platform, CodegenPayload data)
    {
        if (!_generators.TryGetValue(platform, out var generator))
            throw new InvalidOperationException($"Unsupported platform: {platform}");

        var files = generator.GenerateFiles(data)
            .Where(file => file is not null && !string.IsNullOrWhiteSpace(file.Content))
            .Select(file => new CodegenFile
            {
                Path = string.IsNullOrWhiteSpace(file.Path) ? $"{platform}.st" : file.Path,
                Content = file.Content
            })
            .ToList();

        return new CodegenOutput { Files = files };
    }
}
