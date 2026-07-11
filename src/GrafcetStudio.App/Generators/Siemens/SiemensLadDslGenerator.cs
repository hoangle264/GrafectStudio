using GrafcetStudio.Domain.Models;
using GrafcetStudio.App.Expressions;
using HandlebarsDotNet;
using SimaticML.API;
using SimaticML.Blocks;
using SimaticML.Blocks.FlagNet;
using SimaticML.Enums;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using System.Xml;

namespace GrafcetStudio.App.Generators.Siemens;

public sealed class SiemensLadDslGenerator : ICodeGenerator
{
    private static readonly string SiemensLadDebugLogPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "debug.log"));
    //private static void SiemensLadDebugLog(string message)
    //{
    //    try
    //    {
    //        File.AppendAllText(SiemensLadDebugLogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}");
    //    }
    //    catch
    //    {
    //    }
    //}
    private const string DefaultTemplatePath = "templates/siemens-lad/default.lad.hbs";

    public string Platform => "siemens-lad";

    public IEnumerable<CodegenFile> GenerateFiles(CodegenPayload payload)
    {
        var units = payload.Units.Count > 0 ? payload.Units : payload.Unit is null ? [] : [payload.Unit];
        if (units.Count == 0)
        {
            units = [new UnitInfo { Id = "main", Name = payload.Project?.Name ?? "Grafcet" }];
        }

        foreach (var unit in units)
        {
            var unitPayload = CloneForUnit(payload, unit);
            var template = LoadTemplate(payload.TemplateRootPath, unitPayload);
            var document = BuildDocument(template);
            yield return new CodegenFile
            {
                Path = $"{SanitizeBlockName(template.BlockName)}.xml",
                Content = ToString(document)
            };
        }
    }

    private static XmlDocument BuildDocument(SiemensLadTemplate template)
    {
        if (template.TiaVersion > 0)
        {
            SimaticMLAPI.TIA_VERSION = template.TiaVersion;
        }

        var block = new BlockFC();
        block.Init();
        block.AttributeList.BlockName = SanitizeBlockName(template.BlockName);
        if (template.BlockNumber > 0)
        {
            block.AttributeList.BlockNumber = template.BlockNumber;
        }
        block.AttributeList.ProgrammingLanguage = SimaticProgrammingLanguage.LADDER;

        foreach (var network in template.Networks)
        {
            var variableMap = BuildVariables(block, network);
            var segment = new SimaticLADSegment();
            segment.Title[CultureInfo.CurrentCulture] = network.Title;
            segment.Comment[CultureInfo.CurrentCulture] = network.Comment;

            var expression = BuildExpression(network.Expression, variableMap, network);
            var output = BuildOutput(network.Output, variableMap, network);
            _ = segment.Powerrail & (expression & output);
            segment.Create(block);
        }

        return SimaticMLAPI.CreateDocument(block);
    }

    private static Dictionary<string, SimaticVariable> BuildVariables(BlockFC block, SiemensLadNetwork network)
    {
        var refs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        LogicExpressionUtilities.CollectRefs(network.Expression, refs);
        if (!string.IsNullOrWhiteSpace(network.Output?.Ref)) refs.Add(network.Output.Ref);

        return refs.ToDictionary(reference => reference, reference => CreateVariable(block, reference), StringComparer.OrdinalIgnoreCase);
    }

    private static SimaticVariable CreateVariable(BlockFC block, string reference)
    {
        // Rendered LAD text is already concrete ladder text. Treat all refs as global
        // operands unless later DECLARE support maps them to local/temp/instance vars.
        return new SimaticGlobalVariable(reference);
    }

    private static SimaticPart BuildExpression(LogicExpression? expression, IReadOnlyDictionary<string, SimaticVariable> variables, SiemensLadNetwork network)
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

    private static SimaticPart BuildAnd(IList<LogicExpression> nodes, IReadOnlyDictionary<string, SimaticVariable> variables, SiemensLadNetwork network)
    {
        if (nodes.Count == 0)
        {
            throw new InvalidOperationException($"AND expression in network '{network.Id}' must have at least one node.");
        }

        var root = BuildExpression(nodes[0], variables, network);
        for (var index = 1; index < nodes.Count; index++) root &= BuildExpression(nodes[index], variables, network);
        return root;
    }

    private static SimaticPart BuildOr(IList<LogicExpression> nodes, IReadOnlyDictionary<string, SimaticVariable> variables, SiemensLadNetwork network)
    {
        if (nodes.Count == 0)
        {
            throw new InvalidOperationException($"OR expression in network '{network.Id}' must have at least one node.");
        }

        var root = BuildExpression(nodes[0], variables, network);
        for (var index = 1; index < nodes.Count; index++) root |= BuildExpression(nodes[index], variables, network);
        return root;
    }

    private static SimaticPart BuildNot(LogicExpression? node, IReadOnlyDictionary<string, SimaticVariable> variables, SiemensLadNetwork network)
    {
        if (node is null) throw new InvalidOperationException($"NOT expression in network '{network.Id}' is missing node.");
        if (string.Equals(node.Type, "TAG", StringComparison.OrdinalIgnoreCase)) return CreateContact(node.Ref, !node.Negated, variables, network);
        throw new InvalidOperationException($"Network '{network.Id}' has unsupported nested NOT expression. Use negated contacts only.");
    }

    private static ContactPart CreateContact(string? key, bool negated, IReadOnlyDictionary<string, SimaticVariable> variables, SiemensLadNetwork network)
    {
        if (string.IsNullOrWhiteSpace(key) || !variables.TryGetValue(key, out var variable))
        {
            throw new InvalidOperationException($"Network '{network.Id}' references unknown operand '{key}'.");
        }

        return new ContactPart { Operand = variable, Negated = negated };
    }

    private static SimaticPart BuildOutput(SiemensLadOutput? output, IReadOnlyDictionary<string, SimaticVariable> variables, SiemensLadNetwork network)
    {
        if (output is null || string.IsNullOrWhiteSpace(output.Ref))
        {
            throw new InvalidOperationException($"Network '{network.Id}' is missing output instruction.");
        }

        if (!variables.TryGetValue(output.Ref, out var variable))
        {
            throw new InvalidOperationException($"Network '{network.Id}' has invalid output ref '{output.Ref}'.");
        }

        return output.Type?.Trim().ToLowerInvariant() switch
        {
            "coil" => new CoilPart { Operand = variable },
            "set_coil" => new SetCoilPart { Operand = variable },
            "reset_coil" => new ResetCoilPart { Operand = variable },
            var unsupported => throw new InvalidOperationException($"Network '{network.Id}' line {network.OperationLine}: instruction '{unsupported?.ToUpperInvariant()}' is parsed from LAD text but XML generation is not implemented yet.")
        };
    }

    private static SiemensLadTemplate LoadTemplate(string? templateRootPath, CodegenPayload payload)
    {
        var candidates = BuildTemplateCandidates(templateRootPath).ToList();
        var path = candidates.FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException("Cannot find Siemens LAD HBS text template. Tried:" + Environment.NewLine + string.Join(Environment.NewLine, candidates));

        if (!path.EndsWith(".hbs", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Siemens LAD template must be a .hbs text template, not JSON: {path}");
        }

        var source = File.ReadAllText(path);
        //SiemensLadDebugLog("LoadTemplate path=" + path + ", flows=" + payload.Flows.Count + ", units=" + payload.Units.Count);
        //SiemensLadDebugLog("Flow address summary: " + BuildFlowAddressSummary(payload));
        var rendered = RenderTemplateSource(source, payload, path);
        var networkCount = rendered.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
            .Count(line => line.TrimStart().StartsWith("NETWORK ", StringComparison.OrdinalIgnoreCase));
        //SiemensLadDebugLog("Rendered template networkCount=" + networkCount + ", renderedLength=" + rendered.Length + ", preview=" + rendered.Substring(0, Math.Min(600, rendered.Length)).Replace(Environment.NewLine, " "));
        //SiemensLadDebugLog("Rendered network diagnostics:" + Environment.NewLine + BuildRenderedNetworkDiagnostics(rendered));
        return SiemensLadTextTemplateParser.Parse(rendered, path);
    }

    private static string BuildFlowAddressSummary(CodegenPayload payload)
    {
        if (payload.Flows.Count == 0)
        {
            return "<no flows>";
        }

        return string.Join(" || ", payload.Flows.Select(flow =>
        {
            var steps = flow.Steps ?? [];
            var transitions = flow.Transitions ?? [];
            var stepSummary = string.Join(", ", steps.Select(step =>
                (string.IsNullOrWhiteSpace(step.Id) ? "<no-id>" : step.Id)
                + "#" + step.Number
                + " exec=" + QuoteForLog(step.ExecAddress)
                + " done=" + QuoteForLog(step.DoneAddress)));
            var transitionSummary = string.Join(", ", transitions.Select(transition =>
                (string.IsNullOrWhiteSpace(transition.Id) ? "<no-id>" : transition.Id)
                + " cond=" + QuoteForLog(transition.Condition)));

            return "flow=" + QuoteForLog(flow.Name ?? flow.Id)
                + " unit=" + QuoteForLog(flow.Diagram?.UnitId)
                + " activeWordTag=" + QuoteForLog(flow.Diagram?.ActiveWordTag)
                + " completeWordTag=" + QuoteForLog(flow.Diagram?.CompleteWordTag)
                + " steps=[" + stepSummary + "]"
                + " transitions=[" + transitionSummary + "]";
        }));
    }

    private static string BuildRenderedNetworkDiagnostics(string rendered)
    {
        var lines = rendered.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var output = new List<string>();
        var currentNetwork = string.Empty;

        for (var index = 0; index < lines.Length; index++)
        {
            var lineNumber = index + 1;
            var raw = lines[index];
            var trimmed = raw.Trim();
            if (trimmed.StartsWith("NETWORK ", StringComparison.OrdinalIgnoreCase))
            {
                currentNetwork = trimmed.Length > 8 ? trimmed[8..].Trim() : string.Empty;
                output.Add($"line {lineNumber}: {trimmed}");
                continue;
            }

            if (trimmed.StartsWith("EXPR", StringComparison.OrdinalIgnoreCase))
            {
                output.Add($"line {lineNumber}: network={QuoteForLog(currentNetwork)} expr={QuoteForLog(trimmed)} raw={QuoteForLog(raw)}");
            }
            else if (!string.IsNullOrWhiteSpace(currentNetwork) && (trimmed.StartsWith("COIL", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("SET_COIL", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("RESET_COIL", StringComparison.OrdinalIgnoreCase)))
            {
                output.Add($"line {lineNumber}: network={QuoteForLog(currentNetwork)} output={QuoteForLog(trimmed)}");
            }
        }

        return output.Count == 0 ? "<no network diagnostics>" : string.Join(Environment.NewLine, output);
    }

    private static string QuoteForLog(string? value)
        => value is null ? "<null>" : '"' + value.Replace("\r", "\\r").Replace("\n", "\\n") + '"';

    private static string RenderTemplateSource(string source, CodegenPayload payload, string path)
    {
        try
        {
            return Handlebars.Compile(source)(payload);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Invalid Siemens LAD HBS text template: {path}. {ex.Message}", ex);
        }
    }

    private static IEnumerable<string> BuildTemplateCandidates(string? templateRootPath)
    {
        if (!string.IsNullOrWhiteSpace(templateRootPath))
        {
            yield return Path.GetFullPath(Path.Combine(templateRootPath, "siemens-lad.hbs"));
            yield return Path.GetFullPath(Path.Combine(templateRootPath, "default.lad.hbs"));
            yield break;
        }

        yield return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, DefaultTemplatePath));
        yield return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), DefaultTemplatePath));
        yield return Path.GetFullPath(DefaultTemplatePath);
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

public static class SiemensLadTextTemplateParser
{
    public static SiemensLadTemplate Parse(string text, string path = "<rendered Siemens LAD text>")
    {
        var template = new SiemensLadTemplate();
        SiemensLadNetwork? current = null;
        var currentStartLine = 0;
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        for (var index = 0; index < lines.Length; index++)
        {
            var lineNumber = index + 1;
            var raw = lines[index];
            var line = raw.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal) || line.StartsWith("//", StringComparison.Ordinal)) continue;

            var keyword = FirstToken(line).ToUpperInvariant();
            var rest = RestAfterFirstToken(line);

            if (current is null)
            {
                switch (keyword)
                {
                    case "TIA_VERSION":
                        if (!uint.TryParse(rest, out var tiaVersion)) throw Error(path, null, lineNumber, "TIA_VERSION must be an unsigned integer.");
                        template.TiaVersion = tiaVersion;
                        break;
                    case "BLOCK_NAME":
                        if (string.IsNullOrWhiteSpace(rest)) throw Error(path, null, lineNumber, "BLOCK_NAME must not be empty.");
                        template.BlockName = rest.Trim();
                        break;
                    case "BLOCK_NUMBER":
                        if (!uint.TryParse(rest, out var blockNumber)) throw Error(path, null, lineNumber, "BLOCK_NUMBER must be an unsigned integer.");
                        template.BlockNumber = blockNumber;
                        break;
                    case "DECLARE":
                        index = SkipBlock(lines, index, "END_DECLARE", path, null);
                        break;
                    case "NETWORK":
                        if (string.IsNullOrWhiteSpace(rest)) throw Error(path, null, lineNumber, "NETWORK id must not be empty.");
                        current = new SiemensLadNetwork { Id = rest.Trim(), Line = lineNumber };
                        currentStartLine = lineNumber;
                        break;
                    default:
                        throw Error(path, null, lineNumber, $"Unexpected top-level keyword '{keyword}'. Expected TIA_VERSION, BLOCK_NAME, BLOCK_NUMBER, DECLARE, or NETWORK.");
                }

                continue;
            }

            switch (keyword)
            {
                case "TITLE":
                    current.Title = rest;
                    current.TitleLine = lineNumber;
                    break;
                case "COMMENT":
                    current.Comment = string.IsNullOrWhiteSpace(current.Comment) ? rest : current.Comment + Environment.NewLine + rest;
                    break;
                case "STATUS":
                    current.Status = rest.Trim();
                    break;
                case "EXPR":
                    if (string.IsNullOrWhiteSpace(rest)) throw Error(path, current.Id, lineNumber, "EXPR must not be empty.");
                    current.ExpressionLine = lineNumber;
                    try
                    {
                        current.Expression = LogicExpressionParser.Parse(rest.Trim(), SiemensLadTagTokenParser.TagToken, SiemensLadTagTokenParser.ParserOptions);
                    }
                    catch (LogicExpressionParseException ex)
                    {
                        throw Error(path, current.Id, lineNumber, ex.Message);
                    }
                    break;
                case "COIL":
                case "SET_COIL":
                case "RESET_COIL":
                    SetOutput(current, keyword, rest, path, lineNumber);
                    break;
                case "TON":
                case "TOF":
                case "MOVE":
                case "CALL":
                    current.Output = new SiemensLadOutput { Type = keyword.ToLowerInvariant(), Ref = ParseUnsupportedInstructionRef(keyword, rest) };
                    current.OperationLine = lineNumber;
                    current.UnsupportedInstructionText = line;
                    if (keyword is "TON" or "TOF" or "MOVE" or "CALL")
                    {
                        var endKeyword = "END_" + keyword;
                        if (HasBlockTerminator(lines, index + 1, endKeyword, "END"))
                        {
                            index = SkipOptionalInstructionBlock(lines, index, endKeyword);
                        }
                    }
                    break;
                case "END":
                    FinalizeNetwork(template, current, path, currentStartLine, lineNumber);
                    current = null;
                    break;
                default:
                    throw Error(path, current.Id, lineNumber, $"Unexpected keyword '{keyword}' inside NETWORK. Expected TITLE, COMMENT, STATUS, EXPR, COIL, SET_COIL, RESET_COIL, TON, TOF, MOVE, CALL, or END.");
            }
        }

        if (current is not null)
        {
            throw Error(path, current.Id, currentStartLine, "NETWORK is missing END.");
        }

        if (template.Networks.Count == 0)
        {
            throw Error(path, null, 0, "template must contain at least one NETWORK.");
        }

        return template;
    }

    private static void SetOutput(SiemensLadNetwork current, string keyword, string rest, string path, int lineNumber)
    {
        if (current.Output is not null) throw Error(path, current.Id, lineNumber, "network already has an output/instruction line.");
        var reference = ParseRefOperand(rest, path, current.Id, lineNumber, keyword);
        current.Output = new SiemensLadOutput { Type = keyword.ToLowerInvariant(), Ref = reference };
        current.OperationLine = lineNumber;
    }

    private static string ParseRefOperand(string rest, string path, string networkId, int lineNumber, string keyword)
    {
        var tokens = SplitTokens(rest).ToList();
        if (tokens.Count == 0) throw Error(path, networkId, lineNumber, $"{keyword} requires a REF operand.");
        if (tokens[0].Equals("REF", StringComparison.OrdinalIgnoreCase))
        {
            if (tokens.Count < 2) throw Error(path, networkId, lineNumber, $"{keyword} REF operand must not be empty.");
            return tokens[1];
        }

        if (tokens[0].Equals("LIT", StringComparison.OrdinalIgnoreCase))
        {
            throw Error(path, networkId, lineNumber, $"{keyword} expects REF, not LIT.");
        }

        return tokens[0];
    }

    private static void FinalizeNetwork(SiemensLadTemplate template, SiemensLadNetwork network, string path, int startLine, int endLine)
    {
        if (string.IsNullOrWhiteSpace(network.Id)) throw Error(path, null, startLine, "NETWORK id must not be empty.");
        if (network.Expression is null) throw Error(path, network.Id, endLine, "NETWORK is missing EXPR.");
        if (network.Output is null) throw Error(path, network.Id, endLine, "NETWORK is missing output instruction (COIL, SET_COIL, RESET_COIL, TON, TOF, MOVE, or CALL). ");
        template.Networks.Add(network);
    }

    private static string ParseUnsupportedInstructionRef(string keyword, string rest)
    {
        var first = SplitTokens(rest).FirstOrDefault();
        return string.IsNullOrWhiteSpace(first) ? "__unsupported_instruction__" : first;
    }

    private static int SkipBlock(string[] lines, int startIndex, string endKeyword, string path, string? networkId)
    {
        for (var i = startIndex + 1; i < lines.Length; i++)
        {
            if (string.Equals(lines[i].Trim(), endKeyword, StringComparison.OrdinalIgnoreCase)) return i;
        }

        throw Error(path, networkId, startIndex + 1, $"{FirstToken(lines[startIndex].Trim()).ToUpperInvariant()} is missing {endKeyword}.");
    }

    private static int SkipOptionalInstructionBlock(string[] lines, int startIndex, string endKeyword)
    {
        for (var i = startIndex + 1; i < lines.Length; i++)
        {
            var keyword = FirstToken(lines[i].Trim()).ToUpperInvariant();
            if (keyword == endKeyword) return i;
            if (keyword == "END") return startIndex;
        }

        return startIndex;
    }

    private static bool HasBlockTerminator(string[] lines, int startIndex, string endKeyword, string networkEndKeyword)
    {
        for (var i = startIndex; i < lines.Length; i++)
        {
            var keyword = FirstToken(lines[i].Trim()).ToUpperInvariant();
            if (keyword == endKeyword) return true;
            if (keyword == networkEndKeyword || keyword == "NETWORK") return false;
        }

        return false;
    }

    private static string FirstToken(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Length == 0) return string.Empty;
        var index = trimmed.IndexOfAny([' ', '\t']);
        return index < 0 ? trimmed : trimmed[..index];
    }

    private static string RestAfterFirstToken(string line)
    {
        var trimmed = line.Trim();
        var index = trimmed.IndexOfAny([' ', '\t']);
        return index < 0 ? string.Empty : trimmed[(index + 1)..].Trim();
    }

    private static IEnumerable<string> SplitTokens(string value)
        => value.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static InvalidOperationException Error(string path, string? networkId, int line, string message)
    {
        var networkText = string.IsNullOrWhiteSpace(networkId) ? string.Empty : $" Network '{networkId}'.";
        var lineText = line > 0 ? $" line {line}" : string.Empty;
        return new InvalidOperationException($"Invalid Siemens LAD text template: {path}.{networkText}{lineText}: {message}");
    }
}

public sealed class SiemensLadTemplate
{
    [JsonPropertyName("version")] public string Version { get; set; } = "1.0";
    [JsonPropertyName("platform")] public string Platform { get; set; } = "siemens-lad";
    [JsonPropertyName("tiaVersion")] public uint TiaVersion { get; set; } = 19;
    [JsonPropertyName("blockName")] public string BlockName { get; set; } = "Grafcet_LAD";
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
    [JsonPropertyName("expression")] public LogicExpression? Expression { get; set; }
    [JsonPropertyName("output")] public SiemensLadOutput? Output { get; set; }
    public string Status { get; set; } = "ready";
    public int Line { get; set; }
    public int TitleLine { get; set; }
    public int ExpressionLine { get; set; }
    public int OperationLine { get; set; }
    public string UnsupportedInstructionText { get; set; } = string.Empty;
}

public sealed class SiemensLadOutput
{
    [JsonPropertyName("type")] public string Type { get; set; } = "coil";
    [JsonPropertyName("ref")] public string Ref { get; set; } = string.Empty;
}


