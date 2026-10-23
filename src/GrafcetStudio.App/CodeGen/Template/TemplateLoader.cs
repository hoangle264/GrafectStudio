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

    // Alias lists are in priority order: the first file that exists wins and the rest are ignored.
    internal static readonly (string[] Filenames, string Id, bool IsPartial)[] StandardMappings =
    {
        (new[] { "unitError.hbs", "unit-error.hbs", "error.hbs" }, "uc.unitError", false),
        (new[] { "unitManual.hbs", "unit-manual.hbs", "manual.hbs" }, "uc.unitManual", false),
        (new[] { "unitAuto.hbs", "unit-auto.hbs", "auto.hbs" }, "uc.unitAuto", false),
        (new[] { "unitOrigin.hbs", "unit-origin.hbs", "origin.hbs" }, "uc.unitOrigin", false),
        (new[] { "unitMainOutput.hbs", "unit-main-output.hbs", "main-output.hbs" }, "uc.unitMainOutput", false),
        (new[] { "output.hbs" }, "uc.outputLegacy", false),
        (new[] { "step-body.hbs" }, "uc.stepBody", true),
        (new[] { "MapIO.hbs" }, "uc.mapIo", false)
    };

    private static void LoadStandardTemplates(string rootPath, TemplateLoadResult result)
    {
        var mapping = StandardMappings;

        foreach (var item in mapping)
        {
            foreach (var filename in item.Filenames)
            {
                var fullPath = Path.Combine(rootPath, filename);
                if (File.Exists(fullPath))
                {
                    LoadMappedTemplate(fullPath, item.Id, filename, item.IsPartial, result);
                    break;
                }
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
