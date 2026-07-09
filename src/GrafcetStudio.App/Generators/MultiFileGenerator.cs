using GrafcetStudio.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GrafcetStudio.App.Generators;

/// <summary>
/// Composite generator for the unit-config platform.
/// It converts the legacy single-file unit generator into a multi-file payload
/// so the host/UI can preview each artifact independently.
/// </summary>
public sealed class MultiFileGenerator : ICodeGenerator
{
    private readonly UnitConfigGenerator _unitConfig;
    private readonly IErrorGenerator _errorGenerator;
    private readonly IDeviceManagerGenerator _deviceManagerGenerator;
    private readonly ISystemControlGenerator _systemControlGenerator;
    private readonly IMapIOGenerator _mapIoGenerator;

    public MultiFileGenerator(
        UnitConfigGenerator unitConfig,
        IErrorGenerator errorGenerator,
        IDeviceManagerGenerator deviceManagerGenerator,
        ISystemControlGenerator systemControlGenerator,
        IMapIOGenerator mapIoGenerator)
    {
        _unitConfig = unitConfig;
        _errorGenerator = errorGenerator;
        _deviceManagerGenerator = deviceManagerGenerator;
        _systemControlGenerator = systemControlGenerator;
        _mapIoGenerator = mapIoGenerator;
    }

    public string Platform => "unit-config";

    public IEnumerable<CodegenFile> GenerateFiles(CodegenPayload payload)
    {
        foreach (var unitPayload in BuildUnitPayloads(payload))
        {
            var unitName = SanitizeFileToken(unitPayload.Unit?.Label ?? unitPayload.Unit?.Name ?? unitPayload.Project?.Name ?? "Unit");

            yield return new CodegenFile
            {
                Path = $"Units/Unit_{unitName}.st",
                Content = _unitConfig.GenerateUnitContent(unitPayload)
            };
        }

        yield return new CodegenFile
        {
            Path = "Error.st",
            Content = _errorGenerator.Generate(payload)
        };

        yield return new CodegenFile
        {
            Path = "System.st",
            Content = _systemControlGenerator.GenerateSystem(payload)
        };

        if (payload.Flows.Any(flow => string.Equals(flow.Category, "orchestrator", StringComparison.OrdinalIgnoreCase)))
        {
            yield return new CodegenFile
            {
                Path = "Orchestrator.st",
                Content = _systemControlGenerator.GenerateOrchestrator(payload)
            };
        }

        yield return new CodegenFile
        {
            Path = "Devices/DeviceManager.st",
            Content = _deviceManagerGenerator.Generate(payload)
        };

        if ((payload.IOMapping?.PhysicalIOs?.Count ?? 0) > 0 || (payload.IOMapping?.Entries?.Count ?? 0) > 0)
        {
            yield return new CodegenFile
            {
                Path = "Devices/IOMapping.st",
                Content = _mapIoGenerator.Generate(payload, Array.Empty<GrafcetStudio.CodeGen.Runtime.Models.AggregatedOutputBinding>())
            };
        }
    }

    private static IEnumerable<CodegenPayload> BuildUnitPayloads(CodegenPayload payload)
    {
        if (payload.Unit is not null)
        {
            yield return payload;
            yield break;
        }

        var groupedFlows = (payload.Flows ?? new List<FlowInfo>())
            .Where(flow => flow?.Diagram is not null)
            .GroupBy(flow => string.IsNullOrWhiteSpace(flow!.Diagram!.UnitId) ? "__none__" : flow.Diagram.UnitId!, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var group in groupedFlows)
        {
            var sample = group.First();
            var unitId = group.Key;
            var unit = (payload.Units ?? new List<UnitInfo>()).FirstOrDefault(item => string.Equals(item.Id, unitId, StringComparison.OrdinalIgnoreCase));
            var resolvedUnit = unit ?? new UnitInfo
            {
                Id = unitId,
                Name = string.Equals(unitId, "__none__", StringComparison.OrdinalIgnoreCase) ? "No unit" : sample.Diagram?.Unit,
                Label = string.Equals(unitId, "__none__", StringComparison.OrdinalIgnoreCase) ? "No unit" : sample.Diagram?.Unit
            };

            yield return new CodegenPayload
            {
                Platform = payload.Platform,
                TemplateRootPath = payload.TemplateRootPath,
                Project = payload.Project,
                Unit = resolvedUnit,
                Units = payload.Units,
                Flows = group.ToList(),
                Variables = payload.Variables,
                DeviceTypes = payload.DeviceTypes,
                DeviceLibraryPath = payload.DeviceLibraryPath,
                TemplateProfile = payload.TemplateProfile,
                IOMapping = payload.IOMapping,
                UnitConfig = payload.UnitConfig
            };
        }
    }

    private static string SanitizeFileToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Unit";
        return new string(value.Trim().Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray()).Trim('_');
    }
}
