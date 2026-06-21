using GrafcetStudio.CodeGen.Runtime.Models;
using GrafcetStudio.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace GrafcetStudio.App.Generators;

public interface IMapIOGenerator
{
    string Generate(CodegenPayload payload, IEnumerable<AggregatedOutputBinding> bindings);
}

public sealed class MapIOGenerator : IMapIOGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string Generate(CodegenPayload payload, IEnumerable<AggregatedOutputBinding> bindings)
    {
        var deviceTypesByName = payload.DeviceTypes.ToDictionary(deviceType => deviceType.Name, StringComparer.OrdinalIgnoreCase);
        var variablesByLabel = payload.Variables.ToDictionary(variable => variable.Label, StringComparer.OrdinalIgnoreCase);
        var mergedBindings = MergeOutputBindings(bindings);

        var deviceOutputGroups = mergedBindings
            .SelectMany(binding => binding.Sources.Select(source => new { Binding = binding, Source = source }))
            .Where(item => !string.IsNullOrWhiteSpace(item.Source.DeviceLabel))
            .GroupBy(item => item.Source.DeviceLabel, StringComparer.OrdinalIgnoreCase)
            .Select(deviceGroup =>
            {
                var firstSource = deviceGroup.Select(item => item.Source).First();
                variablesByLabel.TryGetValue(deviceGroup.Key, out var variable);
                deviceTypesByName.TryGetValue(firstSource.DeviceFormat, out var deviceType);

                return new
                {
                    deviceLabel = deviceGroup.Key,
                    deviceFormat = firstSource.DeviceFormat,
                    deviceKind = NormalizeDeviceKind(firstSource.DeviceFormat),
                    address = variable?.Address,
                    signalAddresses = variable?.SignalAddresses ?? new Dictionary<string, string>(),
                    unitAddresses = new Dictionary<string, string>(),
                    signals = deviceType?.Signals.Select(signal => new
                    {
                        name = signal.Name,
                        dataType = signal.DataType,
                        varType = signal.VarType.ToString(),
                        comment = signal.Comment,
                        address = variable?.GetSignalAddress(signal.Id) ?? variable?.GetSignalAddress(signal.Name)
                    }).Cast<object>().ToList() ?? new List<object>(),
                    commands = deviceGroup
                        .Where(item => !string.IsNullOrWhiteSpace(item.Source.CommandId))
                        .GroupBy(item => item.Source.CommandId, StringComparer.OrdinalIgnoreCase)
                        .Select(commandGroup => new
                        {
                            commandId = commandGroup.Key,
                            actionLabel = commandGroup.Select(item => item.Source.ActionLabel).FirstOrDefault() ?? string.Empty,
                            driveSignal = commandGroup.Select(item => item.Source.DriveSignal).FirstOrDefault() ?? string.Empty,
                            interlockSignal = commandGroup.Select(item => item.Source.InterlockSignal).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty,
                            interlockAddress = commandGroup.Select(item => item.Source.InterlockAddress).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty,
                            interlockLabel = commandGroup.Select(item => item.Source.InterlockLabel).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty,
                            interlockRequiredState = commandGroup.Select(item => item.Source.InterlockRequiredState).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty,
                            hasInterlock = commandGroup.Any(item => !string.IsNullOrWhiteSpace(item.Source.InterlockSignal) || !string.IsNullOrWhiteSpace(item.Source.InterlockAddress)),
                            physicalOutputRef = commandGroup.Select(item => item.Binding.PhysicalOutputRef).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty,
                            aggregationMode = commandGroup.Select(item => item.Binding.AggregationMode).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "OR"
                        })
                        .OrderBy(command => command.commandId, StringComparer.OrdinalIgnoreCase)
                        .ToList()
                };
            })
            .OrderBy(group => group.deviceLabel, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var context = new
        {
            project = payload.Project,
            unit = new
            {
                id = payload.Unit?.Id ?? string.Empty,
                label = payload.Unit?.Label ?? payload.Unit?.Name ?? payload.Project?.Name ?? "Unit"
            },
            deviceOutputGroups,
            warnings = Array.Empty<string>()
        };

        return JsonSerializer.Serialize(context, JsonOptions);
    }

    private static IList<AggregatedOutputBinding> MergeOutputBindings(IEnumerable<AggregatedOutputBinding> bindings)
    {
        return bindings
            .GroupBy(binding => binding.PhysicalOutputRef, StringComparer.OrdinalIgnoreCase)
            .Select(group => new AggregatedOutputBinding
            {
                PhysicalOutputRef = group.Key,
                SourceExecuteBitRefs = group.SelectMany(binding => binding.SourceExecuteBitRefs).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                SourceDoneBitRefs = group.SelectMany(binding => binding.SourceDoneBitRefs).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                SourceSteps = group.SelectMany(binding => binding.SourceSteps).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                AggregationMode = group.Select(binding => binding.AggregationMode).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "OR",
                Sources = group.SelectMany(binding => binding.Sources).ToList()
            })
            .OrderBy(binding => binding.PhysicalOutputRef, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string NormalizeDeviceKind(string? format)
    {
        if (string.IsNullOrWhiteSpace(format)) return "generic";
        return new string(format.Trim().ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray()).Trim('_');
    }
}
