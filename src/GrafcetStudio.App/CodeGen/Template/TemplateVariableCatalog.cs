using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using GrafcetStudio.App.Generators;
using GrafcetStudio.App.Generators.Common;
using GrafcetStudio.CodeGen.Runtime.Models;
using GrafcetStudio.Domain.Models;
using GrafcetStudio.Domain.Resolution;

namespace GrafcetStudio.CodeGen.Template;

public sealed class TemplateVariableNode
{
    public string Name { get; init; } = string.Empty;
    /// <summary>object | list | string | number | bool | null</summary>
    public string Kind { get; set; } = "null";
    public string? Sample { get; set; }
    public List<TemplateVariableNode> Children { get; } = new();
}

/// <summary>
/// Describes the variables a template can reference, built from the current project payload.
/// Unit templates see the GeneratorContext; Error/System/DeviceManager/MapIO templates see the JSON
/// model their generator emits (see CodeGeneratorService), so the scope depends on the file.
/// </summary>
public static class TemplateVariableCatalog
{
    private const int MaxDepth = 8;
    private const int MaxMergedItems = 200;
    private const int MaxSampleLength = 40;

    private static readonly JsonSerializerOptions ContextJson = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        MaxDepth = 64,
        IncludeFields = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static TemplateVariableNode Build(string relativePath, CodegenPayload payload)
    {
        payload.EnrichVariables();
        var normalized = relativePath.Replace('\\', '/');
        var folder = (Path.GetDirectoryName(normalized) ?? string.Empty).Replace('\\', '/');
        var name = Path.GetFileNameWithoutExtension(normalized).ToLowerInvariant();

        var root = FromJsonText(TryGenerateModel(folder, name, payload));
        if (root is not null) return root;

        var context = GeneratorContextBuilder.Build(payload, new SequenceResolver());
        var tree = FromElement("", JsonSerializer.SerializeToElement(context, ContextJson), 0);

        if (folder is "devices" or "custom-devices") return Descend(tree, "devices") ?? tree;
        if (name == "step-body") return Descend(tree, "flows", "steps") ?? tree;
        return tree;
    }

    private static string? TryGenerateModel(string folder, string name, CodegenPayload payload)
    {
        var isBase = folder is "simple" or "packml" or "";
        if (!isBase) return null;

        return name switch
        {
            "error" when folder != "" => new ErrorGenerator().Generate(payload),
            "system" or "systemcontrol" when folder != "" => new SystemControlGenerator().GenerateSystem(payload),
            "orchestrator" when folder != "" => new SystemControlGenerator().GenerateOrchestrator(payload),
            "devicemanager" when folder != "" => new DeviceManagerGenerator().Generate(payload),
            "mapio" => new MapIOGenerator().Generate(payload, Array.Empty<AggregatedOutputBinding>()),
            _ => null
        };
    }

    private static TemplateVariableNode? FromJsonText(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            return FromElement("", doc.RootElement, 0);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static TemplateVariableNode? Descend(TemplateVariableNode node, params string[] path)
    {
        var current = node;
        foreach (var segment in path)
        {
            current = current.Children.FirstOrDefault(child => string.Equals(child.Name, segment, StringComparison.OrdinalIgnoreCase));
            if (current is null) return null;
        }
        return current;
    }

    private static TemplateVariableNode FromElement(string name, JsonElement element, int depth)
    {
        var node = new TemplateVariableNode { Name = name };
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                node.Kind = "object";
                if (depth >= MaxDepth) break;
                foreach (var property in element.EnumerateObject())
                {
                    node.Children.Add(FromElement(property.Name, property.Value, depth + 1));
                }
                break;
            case JsonValueKind.Array:
                node.Kind = "list";
                if (depth >= MaxDepth) break;
                foreach (var item in element.EnumerateArray().Take(MaxMergedItems))
                {
                    Merge(node, FromElement(string.Empty, item, depth + 1));
                }
                break;
            case JsonValueKind.String:
                node.Kind = "string";
                node.Sample = Truncate(element.GetString());
                break;
            case JsonValueKind.Number:
                node.Kind = "number";
                node.Sample = element.GetRawText();
                break;
            case JsonValueKind.True:
            case JsonValueKind.False:
                node.Kind = "bool";
                node.Sample = element.GetRawText();
                break;
        }
        return node;
    }

    /// <summary>Folds an array element into the list node, taking the union of children.</summary>
    private static void Merge(TemplateVariableNode target, TemplateVariableNode item)
    {
        foreach (var child in item.Children)
        {
            var existing = target.Children.FirstOrDefault(c => string.Equals(c.Name, child.Name, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                target.Children.Add(child);
                continue;
            }

            if (existing.Kind == "null") existing.Kind = child.Kind;
            existing.Sample ??= child.Sample;
            Merge(existing, child);
        }
    }

    private static string? Truncate(string? value)
        => value is null ? null : value.Length <= MaxSampleLength ? value : value[..MaxSampleLength] + "…";
}
