using GrafcetStudio.Domain.Models;
using SimaticML.API;
using SimaticML.Blocks;
using SimaticML.Enums;
using SimaticML.nBlockAttributeList;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;

namespace GrafcetStudio.App.Generators.Siemens;

public sealed class SiemensDbUdtGenerator : ICodeGenerator
{
    private static readonly SimaticDataType STRING = new("String", 0);

    public string Platform => "siemens-db";

    public IEnumerable<CodegenFile> GenerateFiles(CodegenPayload payload)
    {
        var hasBlocks = payload.Blocks.Count > 0;
        var hasSharedStruct = payload.SharedFlowStruct != null && payload.SharedFlowStruct.Enabled;

        if (!hasBlocks && !hasSharedStruct)
        {
            yield break;
        }

        if (hasSharedStruct && payload.SharedFlowStruct != null)
        {
            var structTypeName = SanitizeBlockName(payload.SharedFlowStruct.StructTypeName);
            var udtDoc = BuildSharedFlowUdtDocument(structTypeName, payload.SharedFlowStruct.Members);
            yield return new CodegenFile
            {
                Path = structTypeName + ".xml",
                Content = ToString(udtDoc)
            };

            var flows = payload.Flows ?? new List<FlowInfo>();
            foreach (var flow in flows)
            {
                var flowName = flow.Name;
                if (string.IsNullOrWhiteSpace(flowName)) continue;
                var instanceName = "ST_" + SanitizeBlockName(flowName);
                var dbDoc = BuildFlowInstanceDbDocument(instanceName, structTypeName, payload.SharedFlowStruct.Members);
                yield return new CodegenFile
                {
                    Path = instanceName + ".xml",
                    Content = ToString(dbDoc)
                };
            }
        }

        var variablesByBlock = payload.Variables
            .Where(IsSymbolicBlockVariable)
            .Where(variable => !string.IsNullOrWhiteSpace(variable.BlockId))
            .GroupBy(variable => variable.BlockId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var block in payload.Blocks)
        {
            var kind = (block.Kind ?? string.Empty).Trim();
            if (!string.Equals(kind, "DB", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(kind, "UDT", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var blockName = SanitizeBlockName(block.Name);
            var blockVariables = variablesByBlock.TryGetValue(block.Id, out var list) ? list : [];
            var document = string.Equals(kind, "UDT", StringComparison.OrdinalIgnoreCase)
                ? BuildUdtDocument(blockName, blockVariables, payload.DeviceTypes)
                : BuildGlobalDbDocument(blockName, blockVariables, payload.DeviceTypes);

            yield return new CodegenFile
            {
                Path = blockName + ".xml",
                Content = ToString(document)
            };
        }
    }

    private static XmlDocument BuildSharedFlowUdtDocument(string structTypeName, IEnumerable<SharedFlowStructMember> members)
    {
        var udt = new BlockUDT();
        udt.Init();
        udt.AttributeList.BlockName = structTypeName;

        foreach (var m in members)
        {
            var name = SanitizeMemberName(m.Name);
            var member = udt.AttributeList.NONE.AddMember(name, MapDataType(m.Type));
            if (!string.IsNullOrWhiteSpace(m.Comment))
            {
                member.AddComment(CultureInfo.CurrentCulture, m.Comment);
            }
        }

        return SimaticMLAPI.CreateDocument(udt);
    }

    private static XmlDocument BuildFlowInstanceDbDocument(string instanceName, string structTypeName, IEnumerable<SharedFlowStructMember> members)
    {
        var db = new BlockGlobalDB();
        db.Init();
        db.AttributeList.BlockName = instanceName;

        var structType = new SimaticDataType(structTypeName, 0);
        db.AttributeList.STATIC.AddMember("Data", structType);

        return SimaticMLAPI.CreateDocument(db);
    }

    private static XmlDocument BuildGlobalDbDocument(string blockName, IEnumerable<DeviceVariable> variables, IList<DeviceType> deviceTypes)
    {
        var db = new BlockGlobalDB();
        db.Init();
        db.AttributeList.BlockName = blockName;

        foreach (var variable in variables)
        {
            AddVariableMember(db.AttributeList.STATIC, variable, deviceTypes);
        }

        return SimaticMLAPI.CreateDocument(db);
    }

    private static XmlDocument BuildUdtDocument(string blockName, IEnumerable<DeviceVariable> variables, IList<DeviceType> deviceTypes)
    {
        var udt = new BlockUDT();
        udt.Init();
        udt.AttributeList.BlockName = blockName;

        foreach (var variable in variables)
        {
            AddVariableMember(udt.AttributeList.NONE, variable, deviceTypes);
        }

        return SimaticMLAPI.CreateDocument(udt);
    }

    private static bool IsSymbolicBlockVariable(DeviceVariable variable)
        => string.Equals(variable.DeclarationMode, "SymbolicBlock", StringComparison.OrdinalIgnoreCase);

    private static void AddVariableMember(Section section, DeviceVariable variable, IList<DeviceType> deviceTypes)
    {
        var name = SanitizeMemberName(variable.Label);
        var format = NormalizeTypeName(variable.Format);
        var deviceType = FindDeviceType(deviceTypes, format, null);
        if (deviceType is not null)
        {
            var member = section.AddMember(name, SimaticDataType.STRUCTURE);
            AddDeviceTypeMembers(member, deviceType, deviceTypes, depth: 1);
            return;
        }

        section.AddMember(name, MapDataType(format));
    }

    private static void AddDeviceTypeMembers(Member parent, DeviceType deviceType, IList<DeviceType> deviceTypes, int depth)
    {
        foreach (var signal in deviceType.Signals ?? [])
        {
            var name = SanitizeMemberName(signal.Name);
            var childType = FindDeviceType(deviceTypes, signal.DataType, signal.NestedTypeId);
            if (childType is not null)
            {
                var child = parent.AddMember(name, SimaticDataType.STRUCTURE);
                if (depth < StructLimits.MaxNestingDepth)
                {
                    AddDeviceTypeMembers(child, childType, deviceTypes, depth + 1);
                }
                else
                {
                    child.AddComment(CultureInfo.CurrentCulture, $"Nesting depth limit {StructLimits.MaxNestingDepth} reached; nested members were not expanded.");
                }
                continue;
            }

            parent.AddMember(name, MapDataType(signal.DataType));
        }
    }

    private static DeviceType? FindDeviceType(IEnumerable<DeviceType> deviceTypes, string? dataType, string? nestedTypeId)
    {
        if (!string.IsNullOrWhiteSpace(nestedTypeId))
        {
            var byId = deviceTypes.FirstOrDefault(device => string.Equals(device.Id, nestedTypeId, StringComparison.OrdinalIgnoreCase));
            if (byId is not null) return byId;
        }

        var typeName = NormalizeTypeName(dataType);
        if (string.IsNullOrWhiteSpace(typeName)) return null;
        return deviceTypes.FirstOrDefault(device => string.Equals(device.Name, typeName, StringComparison.OrdinalIgnoreCase));
    }

    private static SimaticDataType MapDataType(string? value)
    {
        var normalized = NormalizeTypeName(value).Replace(" ", string.Empty);
        return normalized.ToUpperInvariant() switch
        {
            "BOOL" or "BOOLEAN" => SimaticDataType.BOOLEAN,
            "BYTE" => SimaticDataType.BYTE,
            "USINT" => SimaticDataType.USINT,
            "WORD" => SimaticDataType.WORD,
            "INT" => SimaticDataType.INT,
            "UINT" => SimaticDataType.UINT,
            "DWORD" or "D_WORD" => SimaticDataType.DWORD,
            "DINT" => SimaticDataType.DINT,
            "UDINT" => SimaticDataType.UDINT,
            "LWORD" or "L_WORD" => SimaticDataType.LWORD,
            "REAL" => SimaticDataType.REAL,
            "LREAL" or "L_REAL" => SimaticDataType.LREAL,
            "TIME" or "TIMER" => SimaticDataType.TIMER,
            "STRING" or "WSTRING" => STRING,
            "COUNTER" => SimaticDataType.COUNTER,
            _ => throw new InvalidOperationException($"Unsupported Siemens DB/UDT data type '{value}'. Define a DeviceType with that name for structs, or use a supported primitive type.")
        };
    }

    private static string NormalizeTypeName(string? value) => (value ?? string.Empty).Trim();

    private static string SanitizeBlockName(string? value)
    {
        var chars = (value ?? string.Empty).Select(ch => char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_').ToArray();
        var result = new string(chars).Trim('_');
        return string.IsNullOrWhiteSpace(result) ? "Grafcet_DB" : result;
    }

    private static string SanitizeMemberName(string? value)
    {
        var chars = (value ?? string.Empty).Trim().Select(ch => char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_').ToArray();
        var result = new string(chars).Trim('_');
        if (string.IsNullOrWhiteSpace(result)) return "Member";
        return char.IsDigit(result[0]) ? "_" + result : result;
    }

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
