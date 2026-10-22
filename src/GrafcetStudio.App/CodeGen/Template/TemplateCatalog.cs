using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GrafcetStudio.CodeGen.Template;

public sealed class TemplateCatalogEntry
{
    public string Path { get; init; } = string.Empty;
    public string Platform { get; init; } = string.Empty;
    public string Group { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool Active { get; init; }
    public string Note { get; init; } = string.Empty;
    public List<string> Ignored { get; init; } = new();
}

/// <summary>
/// Lists the template files <see cref="TemplateLoader"/> and the Siemens LAD generator actually
/// read from a template root, so the editor never offers an alias that the loader would skip.
/// </summary>
public static class TemplateCatalog
{
    private static readonly Dictionary<string, string> Descriptions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["uc.unitError"] = "Unit error handling section",
        ["uc.unitManual"] = "Unit manual mode section",
        ["uc.unitAuto"] = "Unit auto mode section",
        ["uc.unitOrigin"] = "Unit origin (homing) section",
        ["uc.unitMainOutput"] = "Unit main output section",
        ["uc.outputLegacy"] = "Legacy output template",
        ["uc.stepBody"] = "Step body partial shared by unit sections",
        ["uc.mapIo"] = "I/O mapping"
    };

    public static List<TemplateCatalogEntry> List(string rootPath)
    {
        var entries = new List<TemplateCatalogEntry>();
        var root = Path.GetFullPath(rootPath.Trim());
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"Template path not found: {root}");
        }

        foreach (var (filenames, id, _) in TemplateLoader.StandardMappings)
        {
            AddAliasGroup(entries, root, filenames, "Keyence", "Unit templates", Descriptions.GetValueOrDefault(id, string.Empty));
        }

        var profile = TemplateLoader.GetProfileName(rootPath);
        AddFolder(entries, root, "simple", "Keyence", "Base: simple", "Base template (always loaded)", true);
        var packmlActive = string.Equals(profile, "packml", StringComparison.OrdinalIgnoreCase);
        AddFolder(entries, root, "packml", "Keyence", "Base: packml", "PackML base template", packmlActive,
            packmlActive ? string.Empty : "Not used: profile.txt is '" + profile + "', not 'packml'");
        AddFolder(entries, root, "devices", "Keyence", "Device partials", "Device partial", true);
        AddFolder(entries, root, "custom-devices", "Keyence", "Custom device partials", "Custom device partial", true);

        AddAliasGroup(entries, root, new[] { "siemens-lad.hbs", "default.lad.hbs" }, "Siemens LAD", "Siemens LAD", "Ladder text template");
        return entries;
    }

    private static void AddAliasGroup(List<TemplateCatalogEntry> entries, string root, string[] filenames, string platform, string group, string description)
    {
        var existing = filenames.Where(name => File.Exists(System.IO.Path.Combine(root, name))).ToList();
        if (existing.Count == 0) return;

        entries.Add(new TemplateCatalogEntry
        {
            Path = existing[0],
            Platform = platform,
            Group = group,
            Description = description,
            Active = true,
            Ignored = existing.Skip(1).ToList(),
            Note = existing.Count > 1 ? "Ignored aliases: " + string.Join(", ", existing.Skip(1)) : string.Empty
        });
    }

    private static void AddFolder(List<TemplateCatalogEntry> entries, string root, string folder, string platform, string group, string description, bool active, string note = "")
    {
        var path = System.IO.Path.Combine(root, folder);
        if (!Directory.Exists(path)) return;

        foreach (var file in Directory.EnumerateFiles(path, "*.hbs", SearchOption.TopDirectoryOnly).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            entries.Add(new TemplateCatalogEntry
            {
                Path = folder + "/" + System.IO.Path.GetFileName(file),
                Platform = platform,
                Group = group,
                Description = description,
                Active = active,
                Note = note
            });
        }
    }
}
