using GrafcetStudio.CodeGen.Template;
using GrafcetStudio.Domain.Models;
using System;
using System.Linq;
using System.Text.Json;

namespace GrafcetStudio.App.Generators;

public class UnitConfigGenerator : ICodeGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly TemplateManager _templates;

    public UnitConfigGenerator(TemplateManager templates)
    {
        _templates = templates;
    }

    public string Platform => "unit-config";

    public string Generate(CodegenPayload payload)
    {
        var context = BuildContext(payload);
        var templateName = ResolveTemplateName();
        return templateName is null
            ? JsonSerializer.Serialize(context, JsonOptions)
            : _templates.Render(templateName, context);
    }

    private string? ResolveTemplateName()
    {
        if (_templates.IsTemplateLoaded("uc.mainOutput")) return "uc.mainOutput";
        if (_templates.IsTemplateLoaded("uc.outputLegacy")) return "uc.outputLegacy";
        return null;
    }

    private static object BuildContext(CodegenPayload payload)
    {
        var unitId = payload.Diagram?.UnitId ?? string.Empty;
        var unitLabel = !string.IsNullOrWhiteSpace(payload.Diagram?.Unit)
            ? payload.Diagram!.Unit
            : !string.IsNullOrWhiteSpace(payload.Diagram?.UnitId)
                ? payload.Diagram!.UnitId
                : payload.Project?.Name ?? "Unit";

        var deviceTypesByName = payload.DeviceTypes.ToDictionary(d => d.Name, StringComparer.OrdinalIgnoreCase);
        var devices = payload.Variables.Select(variable =>
        {
            deviceTypesByName.TryGetValue(variable.Format, out var deviceType);
            var kind = NormalizeDeviceKind(variable.Format);
            return new
            {
                label = variable.Label,
                name = variable.Label,
                kind,
                format = variable.Format,
                address = variable.Address,
                partialName = $"device_{kind}",
                standardPartialName = ResolveStandardDevicePartial(kind),
                signalAddresses = variable.SignalAddresses,
                signals = deviceType?.Signals.Select(signal => new
                {
                    id = signal.Id,
                    name = signal.Name,
                    dataType = signal.DataType,
                    varType = signal.VarType.ToString(),
                    comment = signal.Comment,
                    address = variable.GetSignalAddress(signal.Id) ?? variable.GetSignalAddress(signal.Name)
                }).ToList()
            };
        }).ToList();

        return new
        {
            project = payload.Project,
            diagram = payload.Diagram,
            unit = new
            {
                id = unitId,
                label = unitLabel,
                unitIndex = 0
            },
            devices,
            cylinders = devices.Where(d => string.Equals(d.kind, "cylinder", StringComparison.OrdinalIgnoreCase)).ToList(),
            steps = payload.Steps,
            transitions = payload.Transitions,
            variables = payload.Variables,
            deviceTypes = payload.DeviceTypes,
            stationFlows = Array.Empty<object>(),
            originSteps = payload.Steps.Where(s => s.IsInitial).ToList(),
            warnings = Array.Empty<string>()
        };
    }

    private static string NormalizeDeviceKind(string? format)
    {
        if (string.IsNullOrWhiteSpace(format)) return "generic";
        return new string(format.Trim().ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray()).Trim('_');
    }

    private static string ResolveStandardDevicePartial(string kind) => kind switch
    {
        "cylinder" => "uc.deviceCylinder",
        "servo" => "uc.deviceServo",
        "motor" => "uc.deviceMotor",
        _ => "uc.deviceGeneric"
    };
}
