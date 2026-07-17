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
                Content = RenderIfTemplateExists(file, data.TemplateProfile, platform, data)
            })
            .ToList();

        return new CodegenOutput { Files = files };
    }

    private string RenderIfTemplateExists(CodegenFile file, string profile, string platform, CodegenPayload payload)
    {
        var path = file.Path?.Replace('\\', '/') ?? string.Empty;
        if (!path.EndsWith(".st", StringComparison.OrdinalIgnoreCase) && !path.EndsWith(".mnm", StringComparison.OrdinalIgnoreCase))
        {
            return file.Content;
        }

        var fileName = System.IO.Path.GetFileNameWithoutExtension(path);
        var folder = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
        var templateKey = ResolveTemplateKey(folder, fileName, profile);
        if (string.IsNullOrWhiteSpace(templateKey) || !_templates.IsTemplateLoaded(templateKey))
        {
            return file.Content;
        }

        var rendered = _templates.Render(templateKey, ParseModel(file.Content));
        if (string.Equals(platform, "Keyence", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(rendered) &&
            (rendered.Contains("->") || rendered.Contains("-&gt;")))
        {
            rendered = Keyence.MnemonicEmitter.ConvertPseudoExpressions(rendered, payload.Variables);
        }

        return rendered;
    }

    private static object ParseModel(string content)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(content);
            return ConvertJsonElement(doc.RootElement.Clone());
        }
        catch
        {
            return content;
        }
    }

    private static object ConvertJsonElement(System.Text.Json.JsonElement element)
    {
        switch (element.ValueKind)
        {
            case System.Text.Json.JsonValueKind.Object:
                var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                foreach (var prop in element.EnumerateObject())
                {
                    dict[prop.Name] = ConvertJsonElement(prop.Value);
                }
                return dict;

            case System.Text.Json.JsonValueKind.Array:
                var list = new List<object>();
                foreach (var item in element.EnumerateArray())
                {
                    list.Add(ConvertJsonElement(item));
                }
                return list;

            case System.Text.Json.JsonValueKind.String:
                return element.GetString() ?? string.Empty;

            case System.Text.Json.JsonValueKind.Number:
                if (element.TryGetInt64(out long l)) return l;
                return element.GetDouble();

            case System.Text.Json.JsonValueKind.True:
                return true;

            case System.Text.Json.JsonValueKind.False:
                return false;

            case System.Text.Json.JsonValueKind.Null:
            default:
                return null!;
        }
    }

    private string? ResolveTemplateKey(string? folder, string fileName, string profile)
    {
        if (string.Equals(folder, "Devices", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(fileName, "DeviceManager", StringComparison.OrdinalIgnoreCase))
            {
                var key = $"{profile}.DeviceManager";
                if (_templates.IsTemplateLoaded(key)) return key;
                return "simple.DeviceManager";
            }
            if (string.Equals(fileName, "IOMapping", StringComparison.OrdinalIgnoreCase))
            {
                var key = $"{profile}.MapIO";
                if (_templates.IsTemplateLoaded(key)) return key;
                if (_templates.IsTemplateLoaded("simple.MapIO")) return "simple.MapIO";
                if (_templates.IsTemplateLoaded("uc.mapIo")) return "uc.mapIo";
            }
        }

        if (string.IsNullOrWhiteSpace(folder))
        {
            if (string.Equals(fileName, "Error", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, "System", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, "Orchestrator", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, "SystemControl", StringComparison.OrdinalIgnoreCase))
            {
                var key = $"{profile}.{fileName}";
                if (_templates.IsTemplateLoaded(key)) return key;
                return $"simple.{fileName}";
            }
        }

        return null;
    }
}
