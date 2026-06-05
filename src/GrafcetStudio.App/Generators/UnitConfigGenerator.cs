using GrafcetStudio.CodeGen.Runtime;
using GrafcetStudio.CodeGen.Runtime.Models;
using GrafcetStudio.CodeGen.Template;
using GrafcetStudio.Domain.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace GrafcetStudio.App.Generators;

public class UnitConfigGenerator : ICodeGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly string[] SectionTemplateOrder =
    [
        "uc.error",
        "uc.manual",
        "uc.origin",
        "uc.auto"
    ];

    private static readonly (string TemplateId, string PartialName)[] KnownPartials =
    [
        ("uc.stepBody", "step_body"),
        ("uc.deviceCylinder", "device_cylinder"),
        ("uc.deviceServo", "device_servo"),
        ("uc.deviceMotor", "device_motor"),
        ("uc.deviceGeneric", "device_generic")
    ];
    private readonly TemplateManager _templates;

    public UnitConfigGenerator(TemplateManager templates)
    {
        _templates = templates;
    }

    public string Platform => "unit-config";
    public string Generate(CodegenPayload payload)
    {
        var context = BuildContext(payload);
//#if DEBUG
 //          System.Diagnostics.Debug.WriteLine(JsonSerializer.Serialize(context, JsonOptions));
//#endif
        RegisterPartials();

        var renderedSections = ResolveSectionTemplateNames()
            .Select(templateName => _templates.TryRender(templateName, context, out var result) ? result : string.Empty)
            .Where(section => !string.IsNullOrWhiteSpace(section))
            .ToList();

        return renderedSections.Count == 0
            ? JsonSerializer.Serialize(context, JsonOptions)
            : string.Join(Environment.NewLine, renderedSections);
    }

    private IEnumerable<string> ResolveSectionTemplateNames()
    {
        foreach (var templateName in SectionTemplateOrder)
        {
            if (_templates.IsTemplateLoaded(templateName)) yield return templateName;
        }

        if (_templates.IsTemplateLoaded("uc.mainOutput"))
        {
            yield return "uc.mainOutput";
        }
        else if (_templates.IsTemplateLoaded("uc.outputLegacy"))
        {
            yield return "uc.outputLegacy";
        }
    }

    private void RegisterPartials()
    {
        foreach (var (templateId, partialName) in KnownPartials)
        {
            RegisterPartialIfLoaded(templateId, partialName);
        }

        foreach (var templateId in _templates.GetLoadedTemplateIds().Where(id => id.StartsWith("device_", StringComparison.OrdinalIgnoreCase)))
        {
            RegisterPartialIfLoaded(templateId, templateId);
        }
    }

    private void RegisterPartialIfLoaded(string templateId, string partialName)
    {
        if (!_templates.IsTemplateLoaded(templateId) || _templates.IsPartialRegistered(partialName)) return;

        var source = _templates.GetTemplateSource(templateId);
        if (!string.IsNullOrEmpty(source)) _templates.RegisterPartial(partialName, source);
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
        var library = LoadDeviceLibrary(payload.DeviceLibraryPath);
        var runtimePlans = flows.Select(flow => RuntimePlanBuilder.Build(flow, payload.Variables, library)).ToList();
        var outputBindings = MergeOutputBindings(runtimePlans.SelectMany(plan => plan.OutputBindingPlan.Bindings));
//#if DEBUG
//        System.Diagnostics.Debug.WriteLine(JsonSerializer.Serialize(outputBindings, JsonOptions));
//#endif
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
            autoFlows,
            originFlows,
            outputBindings,
            warnings = Array.Empty<string>()
        };
    }


    private static IList<AggregatedOutputBinding> MergeOutputBindings(IEnumerable<AggregatedOutputBinding> bindings)
    {
        return bindings
            .GroupBy(binding => binding.PhysicalOutputRef, StringComparer.OrdinalIgnoreCase)
            .Select(group => new AggregatedOutputBinding
            {
                PhysicalOutputRef = group.Key,
                SourceExecuteBitRefs = group
                    .SelectMany(binding => binding.SourceExecuteBitRefs)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                SourceDoneBitRefs = group
                    .SelectMany(binding => binding.SourceDoneBitRefs)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                SourceSteps = group
                    .SelectMany(binding => binding.SourceSteps)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                AggregationMode = group.Select(binding => binding.AggregationMode).FirstOrDefault(mode => !string.IsNullOrWhiteSpace(mode)) ?? "OR",
                Sources = group
                    .SelectMany(binding => binding.Sources)
                    .GroupBy(source => new { Source = source.SourceExecuteBitRef.ToUpperInvariant(), Action = source.ActionSymbol.ToUpperInvariant(), Command = source.CommandId.ToUpperInvariant() })
                    .Select(sourceGroup => sourceGroup.First())
                    .ToList()
            })
            .OrderBy(binding => binding.PhysicalOutputRef, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
    private static DeviceLibraryRoot LoadDeviceLibrary(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return new DeviceLibraryRoot();

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<DeviceLibraryRoot>(json) ?? new DeviceLibraryRoot();
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







