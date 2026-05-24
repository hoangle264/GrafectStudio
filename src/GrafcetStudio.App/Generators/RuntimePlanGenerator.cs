using GrafcetStudio.Domain.Models;
using System.Text.Json;

namespace GrafcetStudio.App.Generators;

public class RuntimePlanGenerator : ICodeGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string Platform => "runtime-plan";

    public string Generate(CodegenPayload payload)
    {
        var sequence = SequenceBuilder.Build(payload).Select((item, index) => new
        {
            index,
            step = item.Step,
            inTransition = item.InTransition,
            outTransition = item.OutTransition,
            execAddress = $"@MR{100 + index * 2}",
            doneAddress = $"@MR{101 + index * 2}"
        });

        return JsonSerializer.Serialize(new
        {
            project = payload.Project,
            unit = payload.Unit,
            platform = payload.Platform,
            counts = new
            {
                flows = payload.Flows.Count,
                steps = payload.Flows.Sum(flow => flow.Steps.Count),
                transitions = payload.Flows.Sum(flow => flow.Transitions.Count),
                variables = payload.Variables.Count,
                sequence = sequence.Count()
            },
            flows = payload.Flows,
            sequence,
            variables = payload.Variables
        }, JsonOptions);
    }
}
