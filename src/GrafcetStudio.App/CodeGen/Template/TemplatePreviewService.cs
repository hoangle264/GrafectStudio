using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GrafcetStudio.App.Generators;
using GrafcetStudio.Domain.Models;
using GrafcetStudio.Domain.Resolution;
using HandlebarsDotNet;

namespace GrafcetStudio.CodeGen.Template;

public sealed class TemplatePreviewResult
{
    public List<CodegenFile> Files { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
}

/// <summary>
/// Renders the real Keyence generator pipeline against the project payload, using the on-disk
/// templates except that the file being edited is replaced by its unsaved text. Everything runs on a
/// throwaway TemplateManager/Handlebars instance so the app-wide registrations are never touched.
/// </summary>
public static class TemplatePreviewService
{
    public static TemplatePreviewResult Render(string templateRoot, string editedFileFullPath, string editedContent, CodegenPayload payload)
    {
        var result = new TemplatePreviewResult();
        var loaded = TemplateLoader.LoadFromPath(templateRoot);
        if (!loaded.IsValid)
        {
            throw new InvalidOperationException(string.Join("; ", loaded.Errors));
        }

        var entries = new List<TemplateEntry>();
        var replaced = false;
        foreach (var entry in loaded.Templates.Values)
        {
            var isEdited = !string.IsNullOrEmpty(entry.FilePath) &&
                string.Equals(Path.GetFullPath(entry.FilePath), editedFileFullPath, StringComparison.OrdinalIgnoreCase);
            replaced |= isEdited;
            entries.Add(isEdited ? WithContent(entry, editedContent) : entry);
        }

        if (!replaced)
        {
            result.Warnings.Add("This file is not loaded by the generator (ignored alias or unused profile), so the preview shows the output without it.");
        }

        var templates = new TemplateManager(Handlebars.Create());
        templates.RegisterTemplates(entries);

        var keyence = new KeyenceGenerator(templates, new SequenceResolver());
        var multiFile = new MultiFileGenerator(keyence, new ErrorGenerator(), new DeviceManagerGenerator(), new SystemControlGenerator(), new MapIOGenerator());
        var service = new CodeGeneratorService(new GrafcetStudio.App.Generators.ICodeGenerator[] { multiFile }, templates);

        payload.TemplateRootPath = templateRoot;
        payload.EnrichVariables();
        result.Files.AddRange(service.Generate("Keyence", payload).Files);
        return result;
    }

    private static TemplateEntry WithContent(TemplateEntry entry, string content) => new()
    {
        Id = entry.Id,
        Name = entry.Name,
        UploadName = entry.UploadName,
        StorageKey = entry.StorageKey,
        PartialName = entry.PartialName,
        CacheKey = entry.CacheKey,
        Description = entry.Description,
        Order = entry.Order,
        AcceptAliases = entry.AcceptAliases,
        Content = content,
        FilePath = entry.FilePath,
        IsCustom = entry.IsCustom,
        IsPartial = entry.IsPartial
    };
}
