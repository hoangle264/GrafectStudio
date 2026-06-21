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

    public MultiFileGenerator(
        UnitConfigGenerator unitConfig,
        IErrorGenerator errorGenerator,
        IDeviceManagerGenerator deviceManagerGenerator,
        ISystemControlGenerator systemControlGenerator)
    {
        _unitConfig = unitConfig;
        _errorGenerator = errorGenerator;
        _deviceManagerGenerator = deviceManagerGenerator;
        _systemControlGenerator = systemControlGenerator;
    }

    public string Platform => "unit-config";

    public IEnumerable<CodegenFile> GenerateFiles(CodegenPayload payload)
    {
        var unitName = SanitizeFileToken(payload.Unit?.Label ?? payload.Unit?.Name ?? payload.Project?.Name ?? "Unit");

        yield return new CodegenFile
        {
            Path = $"Units/Unit_{unitName}.st",
            Content = _unitConfig.GenerateUnitContent(payload)
        };

        yield return new CodegenFile
        {
            Path = "Error.st",
            Content = _errorGenerator.Generate(payload)
        };

        if (payload.Flows.Any(flow => string.Equals(flow.Category, "orchestrator", StringComparison.OrdinalIgnoreCase)))
        {
            yield return new CodegenFile
            {
                Path = "SystemControl.st",
                Content = _systemControlGenerator.Generate(payload)
            };
        }

        yield return new CodegenFile
        {
            Path = "Devices/DeviceManager.st",
            Content = _deviceManagerGenerator.Generate(payload)
        };
    }

    private static string SanitizeFileToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Unit";
        return new string(value.Trim().Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray()).Trim('_');
    }
}
