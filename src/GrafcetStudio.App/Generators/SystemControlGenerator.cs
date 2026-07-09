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
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string GenerateOrchestrator(CodegenPayload payload)
    {
        var orchestratorFlows = payload.Flows
            .Where(flow => string.Equals(flow.Category, "orchestrator", StringComparison.OrdinalIgnoreCase))
            .Select(flow => new
            {
                id = flow.Id,
                name = flow.Name,
                controlState = flow.ControlState ?? flow.Mode ?? string.Empty,
                elements = flow.OrchestratorConfig?.Elements ?? new List<OrchestratorElement>()
            })
            .ToList();

        var content = new
        {
            project = payload.Project,
            unit = payload.Unit,
            orchestratorFlows,
            phase = 1,
            skeleton = true
        };

        return JsonSerializer.Serialize(content, JsonOptions);
    }

    public string GenerateSystem(CodegenPayload payload)
    {
        var content = new
        {
            project = payload.Project,
            unit = payload.Unit,
            units = payload.Units ?? new List<UnitInfo>(),
            flows = payload.Flows ?? new List<FlowInfo>()
        };

        return JsonSerializer.Serialize(content, JsonOptions);
    }
}
