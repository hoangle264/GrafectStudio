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
        // Numeric step mode: a step is active while the flow's step register holds its CASE value,
        // so the "execute bit" is the comparison "<stepVar>=<value>" and there is no done bit.
        var isNumeric = string.Equals(flow.Diagram?.AddressMode, "numeric", StringComparison.OrdinalIgnoreCase);
        var stepVar = !string.IsNullOrWhiteSpace(flow.Diagram?.ActiveWordTag)
            ? flow.Diagram!.ActiveWordTag!.Trim()
            : (flow.Diagram?.ActiveWord ?? "DM0").Trim();

        var stepPlans = flow.Steps.Select(step =>
        {
            if (isNumeric)
            {
                step = WithNumericExecRef(step, stepVar);
            }

            if (string.IsNullOrWhiteSpace(step.ExecAddress))
            {
                throw new InvalidOperationException($"Step '{step.Id}' is missing a pre-resolved execute address.");
            }

            if (!isNumeric && string.IsNullOrWhiteSpace(step.DoneAddress))
            {
                throw new InvalidOperationException($"Step '{step.Id}' is missing a pre-resolved done address.");
            }

            var resolverResults = step.Actions
                .Select(action => action.DeviceCommandResolutionAttempted ? action.ResolvedCommand : DeviceCommandResolver.Resolve(action, step.ExecAddress, variables, library))
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

        var diagram = new DiagramMeta
        {
            Id = flow.Diagram?.Id ?? flow.Id ?? string.Empty,
            Name = flow.Diagram?.Name ?? flow.Name ?? string.Empty,
            Mode = flow.Diagram?.Mode ?? flow.Mode ?? flow.Type ?? string.Empty,
            UnitId = flow.Diagram?.UnitId ?? string.Empty
        };
        var outputBindingPlan = OutputBindingPlanner.Build(stepPlans, diagram);

        return new DiagramRuntimePlan
        {
            Diagram = diagram,
            BaseMr = ParseBaseMrNumber(flow.Diagram?.BaseMr),
            StepPlans = stepPlans,
            OutputBindingPlan = outputBindingPlan
        };
    }

    private static int ParseBaseMrNumber(string? value)
    {
        var text = (value ?? string.Empty).Trim().TrimStart('@');
        var prefixLength = text.TakeWhile(char.IsLetter).Count();
        if (prefixLength <= 0 || prefixLength >= text.Length) return 0;
        return int.TryParse(text[prefixLength..], out var number) ? number : 0;
    }

    private static Step WithNumericExecRef(Step step, string stepVar)
    {
        if (!string.IsNullOrWhiteSpace(step.ExecAddress)) return step;
        return new Step
        {
            Id = step.Id,
            Number = step.Number,
            StepValue = step.StepValue,
            Label = step.Label,
            Kind = step.Kind,
            MacroFlowId = step.MacroFlowId,
            IsInitial = step.IsInitial,
            ExecAddress = $"{stepVar}={step.StepValue}",
            DoneAddress = string.Empty,
            Actions = step.Actions
        };
    }
}
