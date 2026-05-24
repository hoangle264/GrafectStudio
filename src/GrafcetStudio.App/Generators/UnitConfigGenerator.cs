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
        var unitId = payload.Unit?.Id ?? string.Empty;
        var unitLabel = !string.IsNullOrWhiteSpace(payload.Unit?.Label)
            ? payload.Unit!.Label!
            : !string.IsNullOrWhiteSpace(payload.Unit?.Name)
                ? payload.Unit!.Name!
                : payload.Project?.Name ?? "Unit";
        var flows = payload.Flows ?? new();
        var autoFlows = flows.Where(f => string.Equals(NormalizeFlowType(f), "auto", StringComparison.OrdinalIgnoreCase)).ToList();
        var originFlows = flows.Where(f => string.Equals(NormalizeFlowType(f), "origin", StringComparison.OrdinalIgnoreCase)).ToList();

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
            unit = new
            {
                id = unitId,
                label = unitLabel,
                unitIndex = 0
            },
            devices,
            cylinders = devices.Where(d => string.Equals(d.kind, "cylinder", StringComparison.OrdinalIgnoreCase)).ToList(),
            variables = payload.Variables,
            deviceTypes = payload.DeviceTypes,
            stationFlows = autoFlows,
            autoFlows,
            originFlows,
            flows,
            originSteps = originFlows.SelectMany(f => f.Steps).Where(s => s.IsInitial).ToList(),
            warnings = Array.Empty<string>()
        };
    }

    private static string NormalizeFlowType(FlowInfo flow)
    {
        var value = flow.Type ?? flow.Mode ?? string.Empty;
        return string.Equals(value, "origin", StringComparison.OrdinalIgnoreCase) ? "origin" : "auto";
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
