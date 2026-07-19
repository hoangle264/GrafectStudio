using GrafcetStudio.Domain.Models;
using System.Linq;
using System.Text.Json;

namespace GrafcetStudio.App.Generators;

public interface IErrorGenerator
{
    string Generate(CodegenPayload payload);
}

public sealed class ErrorGenerator : IErrorGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string Generate(CodegenPayload payload)
    {
        var errors = payload.Flows
            .SelectMany(flow => flow.Transitions)
            .Select(transition => new
            {
                id = transition.Id,
                label = transition.Label,
                condition = transition.Condition,
                fromStepIds = transition.FromStepIds,
                toStepIds = transition.ToStepIds
            })
            .ToList();

        var devices = (payload.Variables ?? Enumerable.Empty<DeviceVariable>())
            .Select(v => new
            {
                name = v.Label,
                label = v.Label,
                type = v.Format,
                format = v.Format,
                address = v.Address,
                signalAddresses = v.SignalAddresses
            })
            .ToList();

        return JsonSerializer.Serialize(new { errors, devices }, JsonOptions);
    }
}
