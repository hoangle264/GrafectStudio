using GrafcetStudio.App.Generators.Common;
using GrafcetStudio.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace GrafcetStudio.App.Generators;

public interface ISystemControlGenerator
{
    string GenerateOrchestrator(CodegenPayload payload);
    string GenerateSystem(CodegenPayload payload);
}

public sealed class SystemControlGenerator : ISystemControlGenerator
{
    private const string SystemControlFormat = "SystemControl";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>
    /// Finds the single SystemControl struct variable in the payload and maps it.
    /// If none found, returns an empty SystemControlInfo (empty signalAddresses).
    /// </summary>
    private static SystemControlInfo MapSystemControl(IList<DeviceVariable>? variables)
    {
        if (variables == null || variables.Count == 0)
            return new SystemControlInfo();

        var found = variables.FirstOrDefault(v =>
            string.Equals(v.Format, SystemControlFormat, StringComparison.OrdinalIgnoreCase));

        return new SystemControlInfo
        {
            Label = found?.Label ?? string.Empty,
            SignalAddresses = found?.SignalAddresses ?? new Dictionary<string, string>()
        };
    }

    private static object MapUnitBasic(UnitInfo u) => new
    {
        id = u.Id,
        name = u.Name,
        label = u.Label
    };

    public string GenerateOrchestrator(CodegenPayload payload)
    {
        var units = payload.Units ?? new List<UnitInfo>();
        var content = new
        {
            project = payload.Project,
            system = MapSystemControl(payload.Variables),
            units = units.Select(MapUnitBasic).ToList(),
            orchestrators = OrchestratorContextBuilder.Build(
                payload.Flows ?? new List<FlowInfo>(),
                payload.Variables ?? new List<DeviceVariable>(),
                payload.DeviceTypes ?? new List<DeviceType>(),
                units)
        };

        return JsonSerializer.Serialize(content, JsonOptions);
    }

    public string GenerateSystem(CodegenPayload payload)
    {
        var content = new
        {
            project = payload.Project,
            system = MapSystemControl(payload.Variables),
            units = (payload.Units ?? new List<UnitInfo>()).Select(MapUnitBasic).ToList(),
            variables = (payload.Variables ?? new List<DeviceVariable>())
                .Where(v => !string.IsNullOrWhiteSpace(v.Label))
                .GroupBy(v => v.Label, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase)
        };

        return JsonSerializer.Serialize(content, JsonOptions);
    }
}
