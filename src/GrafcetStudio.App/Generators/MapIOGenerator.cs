using GrafcetStudio.CodeGen.Runtime.Models;
using GrafcetStudio.Domain.Models;
using System.Collections.Generic;
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
            warnings = System.Array.Empty<string>()
        };

        return JsonSerializer.Serialize(context, JsonOptions);
    }
}
