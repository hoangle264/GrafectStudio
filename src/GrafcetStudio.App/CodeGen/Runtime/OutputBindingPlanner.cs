using GrafcetStudio.CodeGen.Models;
using GrafcetStudio.CodeGen.Runtime.Models;
using GrafcetStudio.Domain.Enums;

namespace GrafcetStudio.CodeGen.Runtime;

public static class OutputBindingPlanner
{
    public static OutputBindingPlan Build(IList<StepRuntimePlan> stepPlans)
    {
        var diagnostics = new List<Diagnostic>();
        var groups = new Dictionary<string, AggregatedOutputBinding>(StringComparer.OrdinalIgnoreCase);

        foreach (var stepPlan in stepPlans)
        {
            foreach (var binding in stepPlan.OutputBindings)
            {
                if (string.IsNullOrWhiteSpace(binding.PhysicalOutputRef))
                {
                    diagnostics.Add(new Diagnostic
                    {
                        Level = DiagnosticLevel.Warning,
                        Code = "OUTPUT_BINDING_MISSING_OUTPUT",
                        Message = $"Step {stepPlan.StepNumber} has an output binding without a physical output reference."
                    });

                    continue;
                }

                var sourceExecuteBitRef = string.IsNullOrWhiteSpace(binding.SourceExecuteBitRef)
                    ? stepPlan.ExecuteBitRef
                    : binding.SourceExecuteBitRef;

                if (string.IsNullOrWhiteSpace(sourceExecuteBitRef))
                {
                    diagnostics.Add(new Diagnostic
                    {
                        Level = DiagnosticLevel.Warning,
                        Code = "OUTPUT_BINDING_MISSING_SOURCE",
                        Message = $"Step {stepPlan.StepNumber} has an output binding without a source execute bit reference."
                    });

                    continue;
                }

                if (!groups.TryGetValue(binding.PhysicalOutputRef, out var aggregate))
                {
                    aggregate = new AggregatedOutputBinding
                    {
                        PhysicalOutputRef = binding.PhysicalOutputRef,
                        SourceExecuteBitRefs = new List<string>(),
                        SourceSteps = new List<string>(),
                        AggregationMode = "OR",
                        Sources = new List<OutputBindingSource>()
                    };
                    groups[binding.PhysicalOutputRef] = aggregate;
                }

                AddDistinct(aggregate.SourceExecuteBitRefs, sourceExecuteBitRef);

                var sourceStep = string.IsNullOrWhiteSpace(stepPlan.StepLabel)
                    ? stepPlan.StepNumber.ToString()
                    : $"{stepPlan.StepNumber}:{stepPlan.StepLabel}";
                var sourceStepRef = ResolveSourceStepRef(aggregate.SourceSteps, sourceStep, sourceExecuteBitRef);

                AddDistinct(aggregate.SourceSteps, sourceStepRef);

                var source = new OutputBindingSource
                {
                    StepId = stepPlan.StepId,
                    StepNumber = stepPlan.StepNumber,
                    StepLabel = stepPlan.StepLabel,
                    SourceStep = sourceStepRef,
                    SourceExecuteBitRef = sourceExecuteBitRef,
                    ActionSymbol = binding.ActionSymbol,
                    Qualifier = binding.Qualifier,
                    DeviceLabel = binding.DeviceLabel,
                    DeviceFormat = binding.DeviceFormat,
                    CommandId = binding.CommandId,
                    ActionLabel = binding.ActionLabel,
                    DriveSignal = binding.DriveSignal,
                    FeedbackSignals = binding.FeedbackSignals.ToList()
                };

                if (!aggregate.Sources.Any(existing => IsSameSource(existing, source)))
                {
                    aggregate.Sources.Add(source);
                }
            }
        }

        return new OutputBindingPlan
        {
            Bindings = groups.Values
                .OrderBy(binding => binding.PhysicalOutputRef, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Diagnostics = diagnostics
        };
    }

    public static IList<string> Render(OutputBindingPlan plan)
    {
        var lines = new List<string>();

        foreach (var binding in plan.Bindings)
        {
            if (binding.SourceExecuteBitRefs.Count == 0)
            {
                continue;
            }

            lines.Add($"LD {binding.SourceExecuteBitRefs[0]}");

            foreach (var source in binding.SourceExecuteBitRefs.Skip(1))
            {
                lines.Add($"OR {source}");
            }

            lines.Add($"OUT {binding.PhysicalOutputRef}");
        }

        return lines;
    }

    private static void AddDistinct(IList<string> values, string value)
    {
        if (!values.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            values.Add(value);
        }
    }

    private static string ResolveSourceStepRef(IList<string> sourceSteps, string sourceStep, string sourceExecuteBitRef)
    {
        if (!sourceSteps.Contains(sourceStep, StringComparer.OrdinalIgnoreCase))
        {
            return sourceStep;
        }

        var disambiguated = $"{sourceStep} [{sourceExecuteBitRef}]";
        return sourceSteps.Contains(disambiguated, StringComparer.OrdinalIgnoreCase) ? sourceStep : disambiguated;
    }

    private static bool IsSameSource(OutputBindingSource left, OutputBindingSource right)
        => string.Equals(left.SourceExecuteBitRef, right.SourceExecuteBitRef, StringComparison.OrdinalIgnoreCase)
           && string.Equals(left.ActionSymbol, right.ActionSymbol, StringComparison.OrdinalIgnoreCase)
           && string.Equals(left.CommandId, right.CommandId, StringComparison.OrdinalIgnoreCase);
}


