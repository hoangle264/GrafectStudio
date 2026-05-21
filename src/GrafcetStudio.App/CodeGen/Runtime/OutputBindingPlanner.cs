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
                        AggregationMode = "OR"
                    };
                    groups[binding.PhysicalOutputRef] = aggregate;
                }

                if (!aggregate.SourceExecuteBitRefs.Contains(sourceExecuteBitRef, StringComparer.OrdinalIgnoreCase))
                {
                    aggregate.SourceExecuteBitRefs.Add(sourceExecuteBitRef);
                }

                var sourceStep = string.IsNullOrWhiteSpace(stepPlan.StepLabel)
                    ? stepPlan.StepNumber.ToString()
                    : $"{stepPlan.StepNumber}:{stepPlan.StepLabel}";

                if (!aggregate.SourceSteps.Contains(sourceStep, StringComparer.OrdinalIgnoreCase))
                {
                    aggregate.SourceSteps.Add(sourceStep);
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
}
