using System.IO;

void ReplaceLiteral(string path, string from, string to)
{
    var text = File.ReadAllText(path);
    if (text.Contains(from))
    {
        text = text.Replace(from, to);
        File.WriteAllText(path, text);
    }
}

var multi = Path.Combine("src","GrafcetStudio.App","Generators","MultiFileGenerator.cs");
var multiText = File.ReadAllText(multi).Replace("`r`n", "\r\n");
File.WriteAllText(multi, multiText);

ReplaceLiteral(multi,
"    private readonly IDeviceManagerGenerator _deviceManagerGenerator;\r\n    private readonly ISystemControlGenerator _systemControlGenerator;",
"    private readonly IDeviceManagerGenerator _deviceManagerGenerator;\r\n    private readonly ISystemControlGenerator _systemControlGenerator;\r\n    private readonly IMapIOGenerator _mapIoGenerator;");

ReplaceLiteral(multi,
"        IErrorGenerator errorGenerator,\r\n        IDeviceManagerGenerator deviceManagerGenerator,\r\n        ISystemControlGenerator systemControlGenerator)",
"        IErrorGenerator errorGenerator,\r\n        IDeviceManagerGenerator deviceManagerGenerator,\r\n        ISystemControlGenerator systemControlGenerator,\r\n        IMapIOGenerator mapIoGenerator)");

ReplaceLiteral(multi,
"        _errorGenerator = errorGenerator;\r\n        _deviceManagerGenerator = deviceManagerGenerator;\r\n        _systemControlGenerator = systemControlGenerator;",
"        _errorGenerator = errorGenerator;\r\n        _deviceManagerGenerator = deviceManagerGenerator;\r\n        _systemControlGenerator = systemControlGenerator;\r\n        _mapIoGenerator = mapIoGenerator;");

ReplaceLiteral(multi,
"        yield return new CodegenFile\r\n        {\r\n            Path = \"Devices/DeviceManager.st\",\r\n            Content = _deviceManagerGenerator.Generate(payload)\r\n        };",
"        yield return new CodegenFile\r\n        {\r\n            Path = \"Devices/DeviceManager.st\",\r\n            Content = _deviceManagerGenerator.Generate(payload)\r\n        };\r\n\r\n        if ((payload.IOMapping?.PhysicalIOs?.Count ?? 0) > 0 || (payload.IOMapping?.Entries?.Count ?? 0) > 0)\r\n        {\r\n            yield return new CodegenFile\r\n            {\r\n                Path = \"Devices/IOMapping.st\",\r\n                Content = _mapIoGenerator.Generate(payload, Array.Empty<GrafcetStudio.CodeGen.Runtime.Models.AggregatedOutputBinding>())\r\n            };\r\n        }");

ReplaceLiteral(multi,
"                DeviceTypes = payload.DeviceTypes,\r\n                DeviceLibraryPath = payload.DeviceLibraryPath,\r\n                TemplateProfile = payload.TemplateProfile\r\n            };",
"                DeviceTypes = payload.DeviceTypes,\r\n                DeviceLibraryPath = payload.DeviceLibraryPath,\r\n                TemplateProfile = payload.TemplateProfile,\r\n                IOMapping = payload.IOMapping,\r\n                UnitConfig = payload.UnitConfig\r\n            };");

var mapio = Path.Combine("src","GrafcetStudio.App","Generators","MapIOGenerator.cs");
ReplaceLiteral(mapio,
"using System.Linq;\r\nusing System.Text.Json;",
"using System.Linq;\r\nusing System.Text.Json;\r\nusing System.Text.RegularExpressions;");
var mapText = File.ReadAllText(mapio);
if (!mapText.Contains("physicalIoMappings"))
{
    mapText = mapText.Replace("        var context = new\r\n        {\r\n            project = payload.Project,\r\n            unit = new\r\n            {\r\n                id = payload.Unit?.Id ?? string.Empty,\r\n                label = payload.Unit?.Label ?? payload.Unit?.Name ?? payload.Project?.Name ?? \"Unit\"\r\n            },\r\n            deviceOutputGroups,\r\n            warnings = Array.Empty<string>()\r\n        };",
"        var physicalById = (payload.IOMapping?.PhysicalIOs ?? new List<PhysicalIO>())\r\n            .Where(io => !string.IsNullOrWhiteSpace(io.Id))\r\n            .ToDictionary(io => io.Id, StringComparer.OrdinalIgnoreCase);\r\n\r\n        var physicalIoMappings = (payload.IOMapping?.Entries ?? new List<IOMappingEntry>())\r\n            .Where(entry => !string.IsNullOrWhiteSpace(entry.PhysicalIOId))\r\n            .Select(entry =>\r\n            {\r\n                physicalById.TryGetValue(entry.PhysicalIOId, out var physical);\r\n                return new\r\n                {\r\n                    physicalIOId = entry.PhysicalIOId,\r\n                    appVariable = entry.AppVariable,\r\n                    status = entry.Status,\r\n                    matchScore = entry.MatchScore,\r\n                    deviceTag = physical?.DeviceTag ?? string.Empty,\r\n                    plcAddress = physical?.PlcAddress ?? string.Empty,\r\n                    direction = physical?.Direction ?? string.Empty,\r\n                    description = physical?.Description ?? string.Empty,\r\n                    sanitizedVariable = SanitizeToken(entry.AppVariable),\r\n                    sanitizedDeviceTag = SanitizeToken(physical?.DeviceTag ?? entry.PhysicalIOId)\r\n                };\r\n            })\r\n            .ToList();\r\n\r\n        var context = new\r\n        {\r\n            project = payload.Project,\r\n            unit = new\r\n            {\r\n                id = payload.Unit?.Id ?? string.Empty,\r\n                label = payload.Unit?.Label ?? payload.Unit?.Name ?? payload.Project?.Name ?? \"Unit\"\r\n            },\r\n            physicalIoMappings,\r\n            deviceOutputGroups,\r\n            warnings = Array.Empty<string>()\r\n        };");
    mapText = mapText.Replace("    private static string NormalizeDeviceKind(string? format)", "    private static string SanitizeToken(string? value)\r\n    {\r\n        var source = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();\r\n        return Regex.Replace(source, \"[^A-Za-z0-9_]+\", \"_\").Trim('_');\r\n    }\r\n\r\n    private static string NormalizeDeviceKind(string? format)");
    File.WriteAllText(mapio, mapText);
}
