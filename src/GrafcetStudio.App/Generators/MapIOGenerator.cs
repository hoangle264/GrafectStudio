using GrafcetStudio.CodeGen.Runtime.Models;
using GrafcetStudio.Domain.Models;
using GrafcetStudio.App.Generators.Keyence;
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
        var entriesDict = (payload.IOMapping?.Entries ?? new List<IOMappingEntry>())
            .Where(e => !string.IsNullOrEmpty(e.PhysicalIOId) && !string.IsNullOrEmpty(e.AppVariable))
            .ToDictionary(e => e.PhysicalIOId, e => e.AppVariable, StringComparer.OrdinalIgnoreCase);

        var ioMap = (payload.IOMapping?.PhysicalIOs ?? new List<PhysicalIO>())
            .Select(p => {
                entriesDict.TryGetValue(p.Id, out var appVar);
                if (string.IsNullOrEmpty(appVar))
                {
                    return null;
                }

                var dir = (p.Direction ?? string.Empty).Trim().ToLowerInvariant();
                bool isOutput = dir == "output" || dir == "out" || dir == "ouput";
                bool isInput = dir == "input" || dir == "in";

                // Fallback to address prefix if direction is unspecified
                if (!isOutput && !isInput && !string.IsNullOrEmpty(p.PlcAddress))
                {
                    var addressUpper = p.PlcAddress.ToUpperInvariant();
                    if (addressUpper.StartsWith("X"))
                    {
                        isInput = true;
                    }
                    else if (addressUpper.StartsWith("Y"))
                    {
                        isOutput = true;
                    }
                }

                string normalizedDir = isOutput ? "Output" : "Input";

                return new {
                    name = appVar,
                    address = p.PlcAddress ?? string.Empty,
                    direction = normalizedDir
                };
            })
            .Where(x => x != null)
            .ToList();

        var controlState = payload.Flows?.FirstOrDefault(f => !string.IsNullOrEmpty(f.ControlState))?.ControlState ?? string.Empty;
        var units = payload.Units ?? new List<UnitInfo>();
        var errors = (payload.Flows ?? new List<FlowInfo>())
            .SelectMany(flow => flow.Transitions ?? new List<Transition>())
            .Select(transition => new
            {
                id = transition.Id,
                label = transition.Label,
                condition = transition.Condition,
                fromStepIds = transition.FromStepIds,
                toStepIds = transition.ToStepIds
            })
            .ToList();
        var devices = payload.Variables ?? new List<DeviceVariable>();

        var context = new
        {
            project = payload.Project,
            unit = new
            {
                id = payload.Unit?.Id ?? string.Empty,
                label = payload.Unit?.Label ?? payload.Unit?.Name ?? payload.Project?.Name ?? "Unit"
            },
            ioMapping = payload.IOMapping ?? new IOMapping(),
            unitConfig = payload.UnitConfig ?? new Dictionary<string, UnitConfig>(),
            warnings = System.Array.Empty<string>(),

            // Template-compatible fields
            ioMap,
            controlState,
            units,
            errors,
            devices
        };

        return JsonSerializer.Serialize(context, JsonOptions);
    }
}
