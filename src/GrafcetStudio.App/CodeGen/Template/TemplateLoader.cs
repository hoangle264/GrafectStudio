using System;
using System.IO;

namespace GrafcetStudio.CodeGen.Template;

public static class TemplateLoader
{
    public static TemplateLoadResult LoadFromPath(string templateRootPath)
    {
        var result = new TemplateLoadResult();
        if (string.IsNullOrWhiteSpace(templateRootPath))
        {
            result.Warnings.Add("Template root path is empty.");
            return result;
        }

        string rootPath;
        try
        {
            rootPath = Path.GetFullPath(templateRootPath.Trim());
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            result.Errors.Add($"Invalid template path '{templateRootPath}': {ex.Message}");
            return result;
        }

        if (!Directory.Exists(rootPath))
        {
            result.Errors.Add($"Template path not found: {rootPath}");
            return result;
        }

        string profile;
        try
        {
            profile = GetProfileName(templateRootPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            result.Errors.Add($"Error reading profile.txt in '{rootPath}': {ex.Message}");
            return result;
        }

        LoadStandardTemplates(rootPath, result);
        LoadBaseTemplates(rootPath, result, profile);
        LoadDevicePartials(rootPath, result);
        LoadCustomDevicePartials(rootPath, result);

        if (result.LoadedCount == 0)
        {
            result.Warnings.Add($"No .hbs templates loaded from: {rootPath}");
        }

        return result;
    }

    // Each template has exactly one accepted file name.
    internal static readonly (string[] Filenames, string Id, bool IsPartial)[] StandardMappings =
    {
        (new[] { "unitError.hbs" }, "uc.unitError", false),
        (new[] { "unitManual.hbs" }, "uc.unitManual", false),
        (new[] { "unitAuto.hbs" }, "uc.unitAuto", false),
        (new[] { "unitOrigin.hbs" }, "uc.unitOrigin", false),
        (new[] { "unitMainOutput.hbs" }, "uc.unitMainOutput", false),
        (new[] { "step-body.hbs" }, "uc.stepBody", true)
    };

    // Former alias names. They are no longer read, so their presence is reported instead of silently skipped.
    internal static readonly (string Name, string Canonical)[] UnsupportedNames =
    {
        ("unit-error.hbs", "unitError.hbs"),
        ("error.hbs", "unitError.hbs"),
        ("unit-manual.hbs", "unitManual.hbs"),
        ("manual.hbs", "unitManual.hbs"),
        ("unit-auto.hbs", "unitAuto.hbs"),
        ("auto.hbs", "unitAuto.hbs"),
        ("unit-origin.hbs", "unitOrigin.hbs"),
        ("origin.hbs", "unitOrigin.hbs"),
        ("unit-main-output.hbs", "unitMainOutput.hbs"),
        ("main-output.hbs", "unitMainOutput.hbs"),
        ("output.hbs", "unitMainOutput.hbs"),
        ("MapIO.hbs", "simple/MapIO.hbs")
    };

    private static void LoadStandardTemplates(string rootPath, TemplateLoadResult result)
    {
        foreach (var (name, canonical) in UnsupportedNames)
        {
            if (File.Exists(Path.Combine(rootPath, name)))
            {
                result.Errors.Add($"Unsupported template file '{name}'. Rename it to '{canonical}'.");
            }
        }

        foreach (var item in StandardMappings)
        {
            var filename = item.Filenames[0];
            var fullPath = Path.Combine(rootPath, filename);
            if (File.Exists(fullPath))
            {
                LoadMappedTemplate(fullPath, item.Id, filename, item.IsPartial, result);
            }
        }
    }

    private static void LoadBaseTemplates(string rootPath, TemplateLoadResult result, string profile)
    {
        LoadTemplateSet(Path.Combine(rootPath, "simple"), "simple", result);
        if (string.Equals(profile, "packml", StringComparison.OrdinalIgnoreCase))
        {
            LoadTemplateSet(Path.Combine(rootPath, "packml"), "packml", result);
        }
    }

    internal static string GetProfileName(string templateRootPath)
    {
        var profileFile = Path.Combine(Path.GetFullPath(templateRootPath.Trim()), "profile.txt");
        if (!File.Exists(profileFile)) return "simple";
        var profile = File.ReadAllText(profileFile).Trim();
        return string.IsNullOrWhiteSpace(profile) ? "simple" : profile;
    }

    private static void LoadTemplateSet(string folderPath, string prefix, TemplateLoadResult result)
    {
        if (!Directory.Exists(folderPath)) return;

        foreach (var file in Directory.EnumerateFiles(folderPath, "*.hbs", SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var id = $"{prefix}.{name}";
            LoadMappedTemplate(file, id, Path.GetFileName(file), false, result);
        }
    }

    private static void LoadDevicePartials(string rootPath, TemplateLoadResult result)
    {
        var devicesPath = Path.Combine(rootPath, "devices");
        if (!Directory.Exists(devicesPath)) return;

        var standardMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["cylinder"] = "uc.deviceCylinder",
            ["servo"] = "uc.deviceServo",
            ["motor"] = "uc.deviceMotor",
            ["generic"] = "uc.deviceGeneric"
        };

        foreach (var file in Directory.EnumerateFiles(devicesPath, "*.hbs", SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var partialName = $"device_{NormalizePartialName(name)}";
            var id = standardMappings.TryGetValue(name, out var standardId) ? standardId : partialName;
            LoadMappedTemplate(file, id, Path.GetFileName(file), true, result, partialName);
        }
    }

    private static void LoadCustomDevicePartials(string rootPath, TemplateLoadResult result)
    {
        var customPath = Path.Combine(rootPath, "custom-devices");
        if (!Directory.Exists(customPath)) return;

        foreach (var file in Directory.EnumerateFiles(customPath, "*.hbs", SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var id = $"device_{name}";
            LoadMappedTemplate(file, id, Path.GetFileName(file), true, result, id);
        }
    }

    private static string NormalizePartialName(string value)
        => new string(value.Trim().ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray()).Trim('_');

    private static void LoadMappedTemplate(
        string path,
        string id,
        string name,
        bool isPartial,
        TemplateLoadResult result,
        string? partialName = null)
    {
        if (!File.Exists(path)) return;

        try
        {
            var content = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(content))
            {
                result.Warnings.Add($"Template is empty: {path}");
            }

            result.Templates[id] = new TemplateEntry
            {
                Id = id,
                Name = name,
                CacheKey = id,
                PartialName = partialName ?? (isPartial ? id : null),
                Content = content,
                FilePath = path,
                IsCustom = true,
                IsPartial = isPartial
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            result.Errors.Add($"Error loading {path}: {ex.Message}");
        }
    }
}
