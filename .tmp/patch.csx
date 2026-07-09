using System;
using System.IO;

var payload = Path.Combine("src","GrafcetStudio.App","Domain","Models","CodegenPayload.cs");
var text = File.ReadAllText(payload);
if (!text.Contains("[JsonPropertyName(\"ioMapping\")]") )
{
    text = text.Replace("    [JsonPropertyName(\"templateProfile\")]\r\n    public string TemplateProfile { get; set; } = \"simple\";",
@"    [JsonPropertyName(\"templateProfile\")]
    public string TemplateProfile { get; set; } = \"simple\";

    [JsonPropertyName(\"ioMapping\")]
    public IOMapping IOMapping { get; set; } = new();

    [JsonPropertyName(\"unitConfig\")]
    public Dictionary<string, UnitConfig> UnitConfig { get; set; } = new();");

    text += @"

public class UnitConfig
{
    [JsonPropertyName(\"label\")] public string Label { get; set; } = string.Empty;
    [JsonPropertyName(\"signalAddresses\")] public Dictionary<string, string> SignalAddresses { get; set; } = new();
}

public class IOMapping
{
    [JsonPropertyName(\"physicalIOs\")] public List<PhysicalIO> PhysicalIOs { get; set; } = new();
    [JsonPropertyName(\"entries\")] public List<IOMappingEntry> Entries { get; set; } = new();
}

public class PhysicalIO
{
    [JsonPropertyName(\"id\")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName(\"deviceTag\")] public string DeviceTag { get; set; } = string.Empty;
    [JsonPropertyName(\"plcAddress\")] public string PlcAddress { get; set; } = string.Empty;
    [JsonPropertyName(\"direction\")] public string Direction { get; set; } = string.Empty;
    [JsonPropertyName(\"description\")] public string? Description { get; set; }
}

public class IOMappingEntry
{
    [JsonPropertyName(\"physicalIOId\")] public string PhysicalIOId { get; set; } = string.Empty;
    [JsonPropertyName(\"appVariable\")] public string AppVariable { get; set; } = string.Empty;
    [JsonPropertyName(\"status\")] public string Status { get; set; } = string.Empty;
    [JsonPropertyName(\"matchScore\")] public double MatchScore { get; set; }
}
";
    File.WriteAllText(payload, text);
}

var ts = Path.Combine("src","web","ts","types","project.ts");
var tsText = File.ReadAllText(ts);
if (!tsText.Contains("ioMapping: IOMapping;")) throw new Exception("expected ioMapping type present");
if (!tsText.Contains("unitConfig?: Record<string, UnitConfig>;"))
{
    tsText = tsText.Replace("    templateProfile: string;\r\n    // UI-only, not serialized to C#",
"    templateProfile: string;\r\n    ioMapping?: IOMapping;\r\n    unitConfig?: Record<string, UnitConfig>;\r\n    // UI-only, not serialized to C#");
    File.WriteAllText(ts, tsText);
}

var payloadTs = Path.Combine("src","web","ts","codegen","payload.ts");
var payloadTsText = File.ReadAllText(payloadTs);
if (!payloadTsText.Contains("ioMapping: JSON.parse(JSON.stringify((context.project && context.project.ioMapping) || { physicalIOs: [], entries: [] }))"))
{
    payloadTsText = payloadTsText.Replace("      deviceTypes: getCSharpDeviceTypes(context)\n    };",
"      deviceTypes: getCSharpDeviceTypes(context),\n      ioMapping: JSON.parse(JSON.stringify((context.project && context.project.ioMapping) || { physicalIOs: [], entries: [] })),\n      unitConfig: JSON.parse(JSON.stringify((context.project && context.project.unitConfig) || {}))\n    };");
    File.WriteAllText(payloadTs, payloadTsText);
}

var multi = Path.Combine("src","GrafcetStudio.App","Generators","MultiFileGenerator.cs");
var multiText = File.ReadAllText(multi);
if (!multiText.Contains("private readonly IMapIOGenerator _mapIoGenerator;"))
{
    multiText = multiText.Replace("    private readonly ISystemControlGenerator _systemControlGenerator;",
"    private readonly ISystemControlGenerator _systemControlGenerator;\n    private readonly IMapIOGenerator _mapIoGenerator;");
    multiText = multiText.Replace("        IDeviceManagerGenerator deviceManagerGenerator,\n        ISystemControlGenerator systemControlGenerator)",
"        IDeviceManagerGenerator deviceManagerGenerator,\n        ISystemControlGenerator systemControlGenerator,\n        IMapIOGenerator mapIoGenerator)");
    multiText = multiText.Replace("        _deviceManagerGenerator = deviceManagerGenerator;\n        _systemControlGenerator = systemControlGenerator;",
"        _deviceManagerGenerator = deviceManagerGenerator;\n        _systemControlGenerator = systemControlGenerator;\n        _mapIoGenerator = mapIoGenerator;");
    multiText = multiText.Replace("        yield return new CodegenFile\n        {\n            Path = \"Devices/DeviceManager.st\",\n            Content = _deviceManagerGenerator.Generate(payload)\n        };",
@"        yield return new CodegenFile
        {
            Path = \"Devices/DeviceManager.st\",
            Content = _deviceManagerGenerator.Generate(payload)
        };

        if ((payload.IOMapping?.PhysicalIOs?.Count ?? 0) > 0 || (payload.IOMapping?.Entries?.Count ?? 0) > 0)
        {
            var runtimePlans = (payload.Flows ?? new List<FlowInfo>())
                .Select(flow => RuntimePlanBuilder.Build(flow, payload.Variables, new GrafcetStudio.CodeGen.Runtime.Models.DeviceLibraryRoot()))
                .ToList();
            var bindings = runtimePlans.SelectMany(plan => plan.OutputBindingPlan.Bindings).ToList();
            yield return new CodegenFile
            {
                Path = \"Devices/IOMapping.st\",
                Content = _mapIoGenerator.Generate(payload, bindings)
            };
        }");
    multiText = multiText.Replace("                TemplateProfile = payload.TemplateProfile\n            };",
"                TemplateProfile = payload.TemplateProfile,\n                IOMapping = payload.IOMapping,\n                UnitConfig = payload.UnitConfig\n            };");
    File.WriteAllText(multi, multiText);
}
