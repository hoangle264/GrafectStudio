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

        LoadStandardTemplates(rootPath, result);
        LoadDevicePartials(rootPath, result);
        LoadCustomDevicePartials(rootPath, result);

        if (result.LoadedCount == 0)
        {
            result.Warnings.Add($"No .hbs templates loaded from: {rootPath}");
        }

        return result;
    }

    private static void LoadStandardTemplates(string rootPath, TemplateLoadResult result)
    {
        var mapping = new[]
        {
            (Filename: "error.hbs", Id: "uc.error", IsPartial: false),
            (Filename: "manual.hbs", Id: "uc.manual", IsPartial: false),
            (Filename: "auto.hbs", Id: "uc.auto", IsPartial: false),
            (Filename: "origin.hbs", Id: "uc.origin", IsPartial: false),
            (Filename: "main-output.hbs", Id: "uc.mainOutput", IsPartial: false),
            (Filename: "output.hbs", Id: "uc.outputLegacy", IsPartial: false),
            (Filename: "step-body.hbs", Id: "uc.stepBody", IsPartial: true)
        };

        foreach (var item in mapping)
        {
            LoadMappedTemplate(Path.Combine(rootPath, item.Filename), item.Id, item.Filename, item.IsPartial, result);
        }
    }

    private static void LoadDevicePartials(string rootPath, TemplateLoadResult result)
    {
        var devicesPath = Path.Combine(rootPath, "devices");
        if (!Directory.Exists(devicesPath)) return;

        var mapping = new[]
        {
            (Filename: "cylinder.hbs", Id: "uc.deviceCylinder"),
            (Filename: "servo.hbs", Id: "uc.deviceServo"),
            (Filename: "motor.hbs", Id: "uc.deviceMotor"),
            (Filename: "generic.hbs", Id: "uc.deviceGeneric")
        };

        foreach (var item in mapping)
        {
            LoadMappedTemplate(Path.Combine(devicesPath, item.Filename), item.Id, item.Filename, true, result);
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
