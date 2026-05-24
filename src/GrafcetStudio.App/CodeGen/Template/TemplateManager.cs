using System.Collections.Generic;
using HandlebarsDotNet;
using GrafcetStudio.Domain.Enums;

namespace GrafcetStudio.CodeGen.Template;

/// <summary>Manages template registration, rendering, and health checks.</summary>
public class TemplateManager
{
    private readonly IHandlebars _handlebars;
    private readonly Dictionary<string, string> _sources = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HandlebarsTemplate<object, object>> _compiled = new(StringComparer.OrdinalIgnoreCase);

    public TemplateManager(IHandlebars handlebars)
    {
        _handlebars = handlebars;
    }

    public void LoadTemplate(string name, string source)
    {
        _sources[name] = source;
        _compiled[name] = _handlebars.Compile(source);
    }

    public void ResetTemplate(string name)
    {
        _sources.Remove(name);
        _compiled.Remove(name);
    }

    public string? GetTemplateSource(string name)
        => _sources.TryGetValue(name, out var source) ? source : null;

    public bool IsTemplateLoaded(string name) => _compiled.ContainsKey(name);

    public void RegisterBuiltinHelpers()
    {
        _handlebars.RegisterHelper("pad", (writer, context, parameters) =>
        {
            var value = parameters.Length > 0 ? parameters[0]?.ToString() ?? string.Empty : string.Empty;
            writer.WriteSafeString(value.PadRight(12));
        });

        _handlebars.RegisterHelper("eq", (writer, context, parameters) =>
        {
            writer.WriteSafeString(parameters.Length >= 2 && Equals(parameters[0], parameters[1]) ? "true" : "false");
        });

        _handlebars.RegisterHelper("padStart2", (writer, context, parameters) =>
        {
            var value = parameters.Length > 0 ? parameters[0]?.ToString() ?? string.Empty : string.Empty;
            writer.WriteSafeString(value.PadLeft(2, '0'));
        });
    }

    public void RegisterTemplate(TemplateEntry entry)
    {
        var key = ResolveTemplateKey(entry);
        if (entry.IsPartial)
        {
            RegisterPartial(string.IsNullOrWhiteSpace(entry.PartialName) ? key : entry.PartialName!, entry.Content);
            _sources[key] = entry.Content;
            return;
        }

        LoadTemplate(key, entry.Content);
    }

    public void RegisterTemplates(IList<TemplateEntry> entries)
    {
        foreach (var entry in entries) RegisterTemplate(entry);
    }

    public string Render(string templateName, object model)
    {
        if (!_compiled.TryGetValue(templateName, out var template))
        {
            throw new KeyNotFoundException($"Template '{templateName}' is not loaded.");
        }

        return template(model);
    }

    public IList<string> RenderLines(string name, object context)
        => Render(name, context).Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).ToList();

    public TemplateHealth ValidateHealth(string? unitId)
        => CheckHealth();

    public void RegisterPartial(string name, string source)
        => _handlebars.RegisterTemplate(name, source);

    public void RegisterHelper(string name, HandlebarsHelper helper)
        => _handlebars.RegisterHelper(name, helper);

    private static string ResolveTemplateKey(TemplateEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.CacheKey)) return entry.CacheKey!;
        if (!string.IsNullOrWhiteSpace(entry.Id)) return entry.Id;
        return entry.Name;
    }

    public void ApplyCustomTemplatesToCache()
    {
    }

    public void InjectBundledTemplates(IDictionary<string, string> templateBundle, IDictionary<string, string> partialBundle)
    {
        foreach (var partial in partialBundle) RegisterPartial(partial.Key, partial.Value);
        foreach (var template in templateBundle) LoadTemplate(template.Key, template.Value);
    }

    public TemplateHealth CheckHealth()
    {
        var entries = _sources.Select(source => new TemplateHealthEntry
        {
            Id = source.Key,
            Name = source.Key,
            Status = "Bundled",
            Level = DiagnosticLevel.Info,
            Message = "Template loaded.",
            HasSource = !string.IsNullOrWhiteSpace(source.Value)
        }).ToList();

        return new TemplateHealth { Entries = entries };
    }
}
