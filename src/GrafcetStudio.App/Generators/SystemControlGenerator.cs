using GrafcetStudio.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace GrafcetStudio.App.Generators;

public interface ISystemControlGenerator
{
    string Generate(CodegenPayload payload);
}

public sealed class SystemControlGenerator : ISystemControlGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string Generate(CodegenPayload payload)
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
            file = "SystemControl.st",
            project = payload.Project,
            unit = payload.Unit,
            orchestratorFlows,
            template = "orchestrator-flow-wrapper",
            phase = 1,
            skeleton = true
        };

        return JsonSerializer.Serialize(content, JsonOptions);
    }
}
