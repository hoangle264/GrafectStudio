using GrafcetStudio.Domain.Models;
using SimaticML.API;
using SimaticML.Blocks;
using SimaticML.Blocks.FlagNet;
using SimaticML.Enums;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;

namespace GrafcetStudio.App.Generators.Siemens;

public sealed class SiemensLadDslGenerator : ICodeGenerator
{
    private const string DefaultTemplatePath = "templates/siemens-lad/default.lad.json";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public string Platform => "siemens-lad";

    public IEnumerable<CodegenFile> GenerateFiles(CodegenPayload payload)
    {
        var template = LoadTemplate(payload.TemplateRootPath);
        var units = payload.Units.Count > 0 ? payload.Units : payload.Unit is null ? [] : [payload.Unit];
        if (units.Count == 0)
        {
            units = [new UnitInfo { Id = "main", Name = payload.Project?.Name ?? "Grafcet" }];
        }

        foreach (var unit in units)
        {
            var unitPayload = CloneForUnit(payload, unit);
            var document = BuildDocument(template, unitPayload);
            yield return new CodegenFile
            {
                Path = $"{SanitizeBlockName(ResolveBlockName(template, unitPayload))}.xml",
                Content = ToString(document)
            };
        }
    }

    private static XmlDocument BuildDocument(SiemensLadTemplate template, CodegenPayload payload)
    {
        if (template.TiaVersion > 0)
        {
            SimaticMLAPI.TIA_VERSION = template.TiaVersion;
        }

        var block = new BlockFC();
        block.Init();
        block.AttributeList.BlockName = SanitizeBlockName(ResolveBlockName(template, payload));
        if (template.BlockNumber > 0)
        {
            block.AttributeList.BlockNumber = template.BlockNumber;
        }
        block.AttributeList.ProgrammingLanguage = SimaticProgrammingLanguage.LADDER;

        foreach (var network in template.Networks)
        {
            foreach (var context in BuildRenderContexts(payload, network))
            {
                var variableMap = BuildVariables(block, template, context, network);
                var segment = new SimaticLADSegment();
                segment.Title[CultureInfo.CurrentCulture] = ResolveText(network.Title, context);
                segment.Comment[CultureInfo.CurrentCulture] = ResolveText(network.Comment, context);

                var expression = BuildExpression(network.Expression, variableMap, network);
                var output = BuildOutput(network.Output, variableMap, network);
                _ = segment.Powerrail & (expression & output);
                segment.Create(block);
            }
        }

        return SimaticMLAPI.CreateDocument(block);
    }

    private static Dictionary<string, SimaticVariable> BuildVariables(BlockFC block, SiemensLadTemplate template, SiemensLadRenderContext context, SiemensLadNetwork network)
    {
        var result = new Dictionary<string, SimaticVariable>(StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in template.Parameters)
        {
            var address = ResolveParameter(parameter, context);
            if (string.IsNullOrWhiteSpace(address))
            {
                throw new InvalidOperationException($"Siemens LAD parameter '{parameter.Key}' could not be resolved for network '{network.Id}'.");
            }

            result[parameter.Key] = CreateVariable(block, parameter, address);
        }

        return result;
    }

    private static SimaticVariable CreateVariable(BlockFC block, SiemensLadParameter parameter, string address)
    {
        var dataType = ParseDataType(parameter.DataType);
        var scope = parameter.Scope?.Trim().ToLowerInvariant();
        return scope switch
        {
            "input" => block.AttributeList.INPUT.AddVariable(address, dataType),
            "output" => block.AttributeList.OUTPUT.AddVariable(address, dataType),
            "inout" => block.AttributeList.INOUT.AddVariable(address, dataType),
            "temp" => block.AttributeList.TEMP.AddVariable(address, dataType),
            "constant" => block.AttributeList.CONSTANT.AddVariable(address, dataType),
            "global" => new SimaticGlobalVariable(address),
            _ => new SimaticLocalVariable(address)
        };
    }

    private static SimaticDataType ParseDataType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return SimaticDataType.BOOLEAN;
        var dataType = SimaticDataType.FromSimaticMLString(value, false);
        return dataType == SimaticDataType.VOID && !value.Equals("Void", StringComparison.OrdinalIgnoreCase)
            ? SimaticDataType.BOOLEAN
            : dataType;
    }

    private static SimaticPart BuildExpression(SiemensLadExpression? expression, IReadOnlyDictionary<string, SimaticVariable> variables, SiemensLadNetwork network)
    {
        if (expression is null)
        {
            throw new InvalidOperationException($"Network '{network.Id}' is missing expression.");
        }

        var type = expression.Type?.Trim().ToUpperInvariant();
        return type switch
        {
            "TAG" => CreateContact(expression.Ref, expression.Negated, variables, network),
            "AND" => BuildAnd(expression.Nodes, variables, network),
            "OR" => BuildOr(expression.Nodes, variables, network),
            "NOT" => BuildNot(expression.Node, variables, network),
            _ => throw new InvalidOperationException($"Unsupported Siemens LAD expression type '{expression.Type}' in network '{network.Id}'.")
        };
    }

    private static SimaticPart BuildAnd(IList<SiemensLadExpression> nodes, IReadOnlyDictionary<string, SimaticVariable> variables, SiemensLadNetwork network)
    {
        if (nodes.Count == 0)
        {
            throw new InvalidOperationException($"AND expression in network '{network.Id}' must have at least one node.");
        }

        var root = BuildExpression(nodes[0], variables, network);
        for (var index = 1; index < nodes.Count; index++)
        {
            root &= BuildExpression(nodes[index], variables, network);
        }

        return root;
    }

    private static SimaticPart BuildOr(IList<SiemensLadExpression> nodes, IReadOnlyDictionary<string, SimaticVariable> variables, SiemensLadNetwork network)
    {
        if (nodes.Count == 0)
        {
            throw new InvalidOperationException($"OR expression in network '{network.Id}' must have at least one node.");
        }

        var root = BuildExpression(nodes[0], variables, network);
        for (var index = 1; index < nodes.Count; index++)
        {
            root |= BuildExpression(nodes[index], variables, network);
        }

        return root;
    }

    private static SimaticPart BuildNot(SiemensLadExpression? node, IReadOnlyDictionary<string, SimaticVariable> variables, SiemensLadNetwork network)
    {
        if (node is null)
        {
            throw new InvalidOperationException($"NOT expression in network '{network.Id}' is missing node.");
        }

        if (string.Equals(node.Type, "TAG", StringComparison.OrdinalIgnoreCase))
        {
            return CreateContact(node.Ref, !node.Negated, variables, network);
        }

        var part = BuildExpression(node, variables, network);
        return new ContactPart { Operand = new SimaticLocalVariable("__unsupported_nested_not__"), Negated = true } & part;
    }

    private static ContactPart CreateContact(string? key, bool negated, IReadOnlyDictionary<string, SimaticVariable> variables, SiemensLadNetwork network)
    {
        if (string.IsNullOrWhiteSpace(key) || !variables.TryGetValue(key, out var variable))
        {
            throw new InvalidOperationException($"Network '{network.Id}' references unknown parameter '{key}'.");
        }

        return new ContactPart { Operand = variable, Negated = negated };
    }

    private static SimaticPart BuildOutput(SiemensLadOutput? output, IReadOnlyDictionary<string, SimaticVariable> variables, SiemensLadNetwork network)
    {
        if (output is null || string.IsNullOrWhiteSpace(output.Ref) || !variables.TryGetValue(output.Ref, out var variable))
        {
            throw new InvalidOperationException($"Network '{network.Id}' has invalid output ref '{output?.Ref}'.");
        }

        return output.Type?.Trim().ToLowerInvariant() switch
        {
            "set_coil" => new SetCoilPart { Operand = variable },
            "reset_coil" => new ResetCoilPart { Operand = variable },
            _ => new CoilPart { Operand = variable }
        };
    }

    private static SiemensLadTemplate LoadTemplate(string? templateRootPath)
    {
        var candidates = BuildTemplateCandidates(templateRootPath).ToList();
        var path = candidates.FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException("Cannot find Siemens LAD DSL template. Tried:" + Environment.NewLine + string.Join(Environment.NewLine, candidates));

        var template = JsonSerializer.Deserialize<SiemensLadTemplate>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidOperationException($"Invalid Siemens LAD DSL template: {path}");

        ValidateTemplate(template, path);

        return template;
    }

    private static void ValidateTemplate(SiemensLadTemplate template, string path)
    {
        if (!string.Equals(template.Platform, "siemens-lad", StringComparison.OrdinalIgnoreCase))
        {
            throw TemplateValidationError(path, null, "platform", "must be 'siemens-lad'.");
        }

        if (template.Networks.Count == 0)
        {
            throw TemplateValidationError(path, null, "networks", "must contain at least one network.");
        }

        var parameterKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < template.Parameters.Count; index++)
        {
            var key = template.Parameters[index].Key;
            if (string.IsNullOrWhiteSpace(key))
            {
                throw TemplateValidationError(path, null, $"parameters[{index}].key", "must not be empty.");
            }

            if (!parameterKeys.Add(key))
            {
                throw TemplateValidationError(path, null, $"parameters[{index}].key", $"duplicate parameter key '{key}'.");
            }
        }

        for (var index = 0; index < template.Networks.Count; index++)
        {
            ValidateNetwork(template.Networks[index], index, parameterKeys, path);
        }
    }

    private static void ValidateNetwork(SiemensLadNetwork network, int index, IReadOnlySet<string> parameterKeys, string path)
    {
        var networkPath = $"networks[{index}]";
        if (string.IsNullOrWhiteSpace(network.Id))
        {
            throw TemplateValidationError(path, null, $"{networkPath}.id", "must not be empty.");
        }

        var repeat = string.IsNullOrWhiteSpace(network.Repeat) ? "once" : network.Repeat.Trim().ToLowerInvariant();
        if (repeat is not ("once" or "steps" or "transitions" or "actions"))
        {
            throw TemplateValidationError(path, network.Id, $"{networkPath}.repeat", "must be one of: once, steps, transitions, actions.");
        }

        if (network.Expression is null)
        {
            throw TemplateValidationError(path, network.Id, $"{networkPath}.expression", "must not be null.");
        }

        if (network.Output is null)
        {
            throw TemplateValidationError(path, network.Id, $"{networkPath}.output", "must not be null.");
        }
        else if (string.IsNullOrWhiteSpace(network.Output.Ref))
        {
            throw TemplateValidationError(path, network.Id, $"{networkPath}.output.ref", "must not be empty.");
        }
        else if (!parameterKeys.Contains(network.Output.Ref))
        {
            throw TemplateValidationError(path, network.Id, $"{networkPath}.output.ref", $"references unknown parameter '{network.Output.Ref}'.");
        }

        ValidateExpression(network.Expression, parameterKeys, path, network.Id, $"{networkPath}.expression");
    }

    private static void ValidateExpression(SiemensLadExpression expression, IReadOnlySet<string> parameterKeys, string path, string networkId, string jsonPath)
    {
        var type = expression.Type?.Trim().ToUpperInvariant();
        switch (type)
        {
            case "TAG":
                if (string.IsNullOrWhiteSpace(expression.Ref))
                {
                    throw TemplateValidationError(path, networkId, $"{jsonPath}.ref", "TAG expression must have a non-empty ref.");
                }

                if (!parameterKeys.Contains(expression.Ref))
                {
                    throw TemplateValidationError(path, networkId, $"{jsonPath}.ref", $"TAG references unknown parameter '{expression.Ref}'.");
                }
                break;

            case "AND":
            case "OR":
                if (expression.Nodes.Count == 0)
                {
                    throw TemplateValidationError(path, networkId, $"{jsonPath}.nodes", $"{type} expression must have at least one node.");
                }

                for (var index = 0; index < expression.Nodes.Count; index++)
                {
                    ValidateExpression(expression.Nodes[index], parameterKeys, path, networkId, $"{jsonPath}.nodes[{index}]");
                }
                break;

            case "NOT":
                if (expression.Node is null)
                {
                    throw TemplateValidationError(path, networkId, $"{jsonPath}.node", "NOT expression must have a node.");
                }

                if (!string.Equals(expression.Node.Type, "TAG", StringComparison.OrdinalIgnoreCase))
                {
                    throw TemplateValidationError(path, networkId, $"{jsonPath}.node.type", "NOT currently supports only TAG nodes.");
                }

                ValidateExpression(expression.Node, parameterKeys, path, networkId, $"{jsonPath}.node");
                break;

            default:
                throw TemplateValidationError(path, networkId, $"{jsonPath}.type", $"unsupported expression type '{expression.Type}'.");
        }
    }

    private static InvalidOperationException TemplateValidationError(string path, string? networkId, string jsonPath, string message)
    {
        var networkText = string.IsNullOrWhiteSpace(networkId) ? string.Empty : $" Network '{networkId}'.";
        return new InvalidOperationException($"Invalid Siemens LAD DSL template: {path}.{networkText} JSON path '{jsonPath}' {message}");
    }
    private static IEnumerable<string> BuildTemplateCandidates(string? templateRootPath)
    {
        if (!string.IsNullOrWhiteSpace(templateRootPath))
        {
            yield return Path.GetFullPath(Path.Combine(templateRootPath, "siemens-lad.json"));
            yield return Path.GetFullPath(Path.Combine(templateRootPath, "default.lad.json"));
        }

        yield return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, DefaultTemplatePath));
        yield return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), DefaultTemplatePath));
        yield return Path.GetFullPath(DefaultTemplatePath);
    }

    private static IEnumerable<SiemensLadRenderContext> BuildRenderContexts(CodegenPayload payload, SiemensLadNetwork network)
    {
        var repeat = string.IsNullOrWhiteSpace(network.Repeat) ? "once" : network.Repeat.Trim().ToLowerInvariant();
        return repeat switch
        {
            "once" => [new SiemensLadRenderContext(payload, payload.Project, payload.Unit, payload.Flows.FirstOrDefault())],
            "steps" => payload.Flows.SelectMany(flow => flow.Steps.Select(step => new SiemensLadRenderContext(payload, payload.Project, payload.Unit, flow, step))).ToList(),
            "transitions" => payload.Flows.SelectMany(flow => flow.Transitions.Select(transition => new SiemensLadRenderContext(payload, payload.Project, payload.Unit, flow, Transition: transition))).ToList(),
            "actions" => payload.Flows.SelectMany(flow => flow.Steps.SelectMany(step => step.Actions.Select(action => new SiemensLadRenderContext(payload, payload.Project, payload.Unit, flow, step, Action: action)))).ToList(),
            _ => throw new InvalidOperationException($"Unsupported Siemens LAD repeat value '{network.Repeat}' in network '{network.Id}'.")
        };
    }

    private static string ResolveParameter(SiemensLadParameter parameter, SiemensLadRenderContext context)
    {
        var source = parameter.Source?.Trim();
        if (string.IsNullOrWhiteSpace(source)) return parameter.Default ?? parameter.Key;

        if (source.StartsWith("step.", StringComparison.OrdinalIgnoreCase))
        {
            return source[5..] switch
            {
                "execAddress" => context.Step?.ExecAddress ?? parameter.Default ?? parameter.Key,
                "doneAddress" => context.Step?.DoneAddress ?? parameter.Default ?? parameter.Key,
                _ => parameter.Default ?? parameter.Key
            };
        }

        if (source.StartsWith("action.", StringComparison.OrdinalIgnoreCase))
        {
            return source[7..] switch
            {
                "address" => context.Action?.ToPhysicalAddress(context.Payload.Variables) ?? parameter.Default ?? parameter.Key,
                "variable" => context.Action?.Variable ?? parameter.Default ?? parameter.Key,
                _ => parameter.Default ?? parameter.Key
            };
        }

        if (source.StartsWith("transition.", StringComparison.OrdinalIgnoreCase))
        {
            return source[11..] switch
            {
                "condition" => context.Transition?.ResolveAddress(context.Payload.Variables) ?? parameter.Default ?? parameter.Key,
                _ => parameter.Default ?? parameter.Key
            };
        }

        return parameter.Default ?? source;
    }

    private static string ResolveBlockName(SiemensLadTemplate template, CodegenPayload payload)
    {
        var context = new SiemensLadRenderContext(payload, payload.Project, payload.Unit, payload.Flows.FirstOrDefault());
        var name = ResolveText(template.BlockName, context);
        return string.IsNullOrWhiteSpace(name) ? "Grafcet_LAD" : name;
    }

    private static string ResolveText(string? template, SiemensLadRenderContext context)
    {
        var value = template ?? string.Empty;
        var unitName = context.Unit?.Label ?? context.Unit?.Name ?? context.Project?.Name ?? "Unit";
        return value
            .Replace("{{project.name}}", context.Project?.Name ?? "Project", StringComparison.OrdinalIgnoreCase)
            .Replace("{{unit.name}}", unitName, StringComparison.OrdinalIgnoreCase)
            .Replace("{{flow.name}}", context.Flow?.Name ?? context.Flow?.Diagram?.Name ?? context.Flow?.Id ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{{step.number}}", context.Step?.Number.ToString(CultureInfo.InvariantCulture) ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{{step.label}}", context.Step?.Label ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{{transition.label}}", context.Transition?.Label ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{{action.variable}}", context.Action?.Variable ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static string SanitizeBlockName(string value)
    {
        var chars = value.Select(ch => char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_').ToArray();
        var result = new string(chars).Trim('_');
        return string.IsNullOrWhiteSpace(result) ? "Grafcet_LAD" : result;
    }

    private static CodegenPayload CloneForUnit(CodegenPayload payload, UnitInfo unit)
        => new()
        {
            Platform = payload.Platform,
            TemplateRootPath = payload.TemplateRootPath,
            Project = payload.Project,
            Unit = unit,
            Units = [unit],
            Flows = payload.Flows.Where(flow => string.IsNullOrWhiteSpace(flow.Diagram?.UnitId) || string.Equals(flow.Diagram.UnitId, unit.Id, StringComparison.OrdinalIgnoreCase)).ToList(),
            Variables = payload.Variables,
            DeviceTypes = payload.DeviceTypes,
            DeviceLibraryPath = payload.DeviceLibraryPath,
            TemplateProfile = payload.TemplateProfile
        };

    private static string ToString(XmlDocument document)
    {
        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            NewLineChars = Environment.NewLine,
            NewLineHandling = NewLineHandling.Replace,
            OmitXmlDeclaration = false
        };

        using var writer = new StringWriter();
        using var xmlWriter = XmlWriter.Create(writer, settings);
        document.WriteContentTo(xmlWriter);
        xmlWriter.Flush();
        return writer.ToString();
    }
}

public sealed record SiemensLadRenderContext(CodegenPayload Payload, ProjectInfo? Project, UnitInfo? Unit, FlowInfo? Flow = null, Step? Step = null, Transition? Transition = null, StepAction? Action = null);

public sealed class SiemensLadTemplate
{
    [JsonPropertyName("version")] public string Version { get; set; } = "1.0";
    [JsonPropertyName("platform")] public string Platform { get; set; } = "siemens-lad";
    [JsonPropertyName("tiaVersion")] public uint TiaVersion { get; set; } = 19;
    [JsonPropertyName("blockName")] public string BlockName { get; set; } = "{{unit.name}}_Grafcet";
    [JsonPropertyName("blockNumber")] public uint BlockNumber { get; set; } = 1;
    [JsonPropertyName("parameters")] public List<SiemensLadParameter> Parameters { get; set; } = [];
    [JsonPropertyName("networks")] public List<SiemensLadNetwork> Networks { get; set; } = [];
}

public sealed class SiemensLadParameter
{
    [JsonPropertyName("key")] public string Key { get; set; } = string.Empty;
    [JsonPropertyName("source")] public string Source { get; set; } = string.Empty;
    [JsonPropertyName("default")] public string? Default { get; set; }
    [JsonPropertyName("scope")] public string Scope { get; set; } = "global";
    [JsonPropertyName("dataType")] public string DataType { get; set; } = "Boolean";
}

public sealed class SiemensLadNetwork
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("repeat")] public string Repeat { get; set; } = "once";
    [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
    [JsonPropertyName("comment")] public string Comment { get; set; } = string.Empty;
    [JsonPropertyName("expression")] public SiemensLadExpression? Expression { get; set; }
    [JsonPropertyName("output")] public SiemensLadOutput? Output { get; set; }
}

public sealed class SiemensLadExpression
{
    [JsonPropertyName("type")] public string Type { get; set; } = "TAG";
    [JsonPropertyName("ref")] public string? Ref { get; set; }
    [JsonPropertyName("negated")] public bool Negated { get; set; }
    [JsonPropertyName("node")] public SiemensLadExpression? Node { get; set; }
    [JsonPropertyName("nodes")] public List<SiemensLadExpression> Nodes { get; set; } = [];
}

public sealed class SiemensLadOutput
{
    [JsonPropertyName("type")] public string Type { get; set; } = "coil";
    [JsonPropertyName("ref")] public string Ref { get; set; } = string.Empty;
}
