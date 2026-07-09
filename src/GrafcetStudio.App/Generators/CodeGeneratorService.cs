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
                Content = RenderIfTemplateExists(file)
            })
            .ToList();

        return new CodegenOutput { Files = files };
    }

    private string RenderIfTemplateExists(CodegenFile file)
    {
        var path = file.Path?.Replace('\\', '/') ?? string.Empty;
        if (!path.EndsWith(".st", StringComparison.OrdinalIgnoreCase))
        {
            return file.Content;
        }

        var fileName = System.IO.Path.GetFileNameWithoutExtension(path);
        var folder = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
        var templateKey = ResolveTemplateKey(folder, fileName);
        if (string.IsNullOrWhiteSpace(templateKey) || !_templates.IsTemplateLoaded(templateKey))
        {
            return file.Content;
        }

        return _templates.Render(templateKey, ParseModel(file.Content));
    }

    private static object ParseModel(string content)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<object>(content) ?? new { };
        }
        catch
        {
            return content;
        }
    }

    private static string? ResolveTemplateKey(string? folder, string fileName)
    {
        if (string.Equals(folder, "Devices", StringComparison.OrdinalIgnoreCase) && string.Equals(fileName, "DeviceManager", StringComparison.OrdinalIgnoreCase))
        {
            return "simple.DeviceManager";
        }

        if (string.IsNullOrWhiteSpace(folder))
        {
            if (string.Equals(fileName, "Error", StringComparison.OrdinalIgnoreCase)) return "simple.Error";
            if (string.Equals(fileName, "System", StringComparison.OrdinalIgnoreCase)) return "simple.System";
            if (string.Equals(fileName, "Orchestrator", StringComparison.OrdinalIgnoreCase)) return "simple.Orchestrator";
            if (string.Equals(fileName, "SystemControl", StringComparison.OrdinalIgnoreCase)) return "simple.SystemControl";
        }

        return null;
    }
}
