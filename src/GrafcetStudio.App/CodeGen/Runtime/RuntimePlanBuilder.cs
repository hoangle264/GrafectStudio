using GrafcetStudio.CodeGen.Runtime.Models;
using GrafcetStudio.Domain.Models;

namespace GrafcetStudio.CodeGen.Runtime;

public static class RuntimePlanBuilder
{
    public static DiagramRuntimePlan Build(
        FlowInfo flow,
        IList<DeviceVariable> variables,
        DeviceLibraryRoot library)
    {
        var stepPlans = flow.Steps.Select(step =>
        {
            if (string.IsNullOrWhiteSpace(step.ExecAddress))
            {
                throw new InvalidOperationException($"Step '{step.Id}' is missing a pre-resolved execute address.");
            }

            if (string.IsNullOrWhiteSpace(step.DoneAddress))
            {
                throw new InvalidOperationException($"Step '{step.Id}' is missing a pre-resolved done address.");
            }

            var resolverResults = step.Actions
                .Select(action => DeviceCommandResolver.Resolve(action, step.ExecAddress, variables, library))
                .Where(result => result is not null)
                .Cast<ActionResolveResult>()
                .ToList();

            return new StepRuntimePlan
            {
                StepId = step.Id,
                StepNumber = step.Number,
                StepLabel = step.Label,
                ExecuteBitRef = step.ExecAddress,
                DoneBitRef = step.DoneAddress,
                ResolverResults = resolverResults,
                FeedbackRefs = resolverResults
                    .SelectMany(result => result.FeedbackSignals)
                    .Select(signal => signal.PhysicalAddress)
                    .Where(address => !string.IsNullOrWhiteSpace(address))
                    .ToList(),
                OutputBindings = resolverResults.SelectMany(result => result.OutputBindings).ToList(),
                Diagnostics = resolverResults.SelectMany(result => result.Diagnostics).ToList()
            };
        }).ToList();

        var outputBindingPlan = OutputBindingPlanner.Build(stepPlans);

        return new DiagramRuntimePlan
        {
            Diagram = new DiagramMeta
            {
                Id = flow.Diagram?.Id ?? flow.Id ?? string.Empty,
                Name = flow.Diagram?.Name ?? flow.Name ?? string.Empty,
                Mode = flow.Diagram?.Mode ?? flow.Mode ?? flow.Type ?? string.Empty,
                UnitId = flow.Diagram?.UnitId ?? string.Empty
            },
            BaseMr = flow.Diagram?.BaseMr ?? 0,
            StepPlans = stepPlans,
            OutputBindingPlan = outputBindingPlan
        };
    }
}
