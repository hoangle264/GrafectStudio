using System.Collections.Generic;
using System.Text;
using GrafcetStudio.CodeGen.Models;
using GrafcetStudio.Domain.Models;
using GrafcetStudio.Domain.Resolution;

namespace GrafcetStudio.CodeGen;

/// <summary>Generates ST preview code for diagrams.</summary>
public class StGenerator : ICodeGenerator
{
    private readonly IProjectRepository _repo;
    private readonly ISequenceResolver _sequenceResolver;

    public StGenerator(IProjectRepository repo, ISequenceResolver sequenceResolver)
    {
        _repo = repo;
        _sequenceResolver = sequenceResolver;
    }

    public GenerationResult GenerateAll(IList<string> diagIds, GenerationOptions opts)
    {
        var lines = new List<string>();
        var diagnostics = new List<Diagnostic>();
        var totalSteps = 0;

        foreach (var diagId in diagIds)
        {
            var meta = _repo.GetDiagramMeta(diagId);
            var state = _repo.LoadDiagramState(diagId);
            if (meta is null || state is null)
            {
                diagnostics.Add(new Diagnostic { Level = Domain.Enums.DiagnosticLevel.Error, Code = "DIAG_NOT_FOUND", Message = $"Diagram '{diagId}' was not found." });
                continue;
            }

            var result = GenerateDiagram(meta, state, opts);
            lines.AddRange(result.Lines);
            lines.Add(string.Empty);
            totalSteps += result.StepCount;
        }

        return new GenerationResult
        {
            Code = string.Join(Environment.NewLine, lines),
            Stats = $"Diagrams: {diagIds.Count}, Steps: {totalSteps}",
            Diagnostics = diagnostics
        };
    }

    public DiagramResult GenerateDiagram(DiagramMeta meta, DiagramState state, GenerationOptions opts)
    {
        var sequence = _sequenceResolver.Resolve(state);
        var lines = new List<string> { $"// Diagram: {meta.Name} ({meta.Mode})" };

        foreach (var entry in sequence)
        {
            var prev = entry.Step.IsInitial ? new List<string> { "TRUE" } : new List<string> { $"S{Math.Max(1, entry.Step.Number - 1):D2}_Done" };
            lines.AddRange(RenderStepLogic(new StStepRenderParams
            {
                StepNum = entry.Step.Number.ToString("D2"),
                PrevDoneVars = prev,
                ActivationCond = NormalizeCondition(entry.InTransition?.Condition),
                FeedbackCond = NormalizeCondition(entry.OutTransition?.Condition),
                StepLabel = entry.Step.Label
            }));

            foreach (var action in entry.Step.Actions.Where(a => a.Qualifier == Domain.Enums.ActionQualifier.N))
            {
                lines.Add($"{action.ToPhysicalAddress(state.Variables)} := S{entry.Step.Number:D2}_Exec;");
            }

            lines.Add(string.Empty);
        }

        return new DiagramResult { Lines = lines, StepCount = sequence.Count };
    }

    public IList<string> GenerateOutputSection(IList<DiagramEntry> entries, IDictionary<string, IList<SignalActionEntry>> signalActionMap)
        => signalActionMap.Select(pair => $"{pair.Key} := {string.Join(" OR ", pair.Value.Select(a => a.ExecMr))};").ToList();

    public IList<string> RenderStepLogic(StStepRenderParams p)
    {
        var prev = p.PrevDoneVars.Count == 0 ? "TRUE" : string.Join(" AND ", p.PrevDoneVars);
        var activation = string.IsNullOrWhiteSpace(p.ActivationCond) ? prev : $"({prev}) AND ({p.ActivationCond})";
        var feedback = string.IsNullOrWhiteSpace(p.FeedbackCond) ? $"S{p.StepNum}_Exec" : $"S{p.StepNum}_Exec AND ({p.FeedbackCond})";

        return new List<string>
        {
            $"// S{p.StepNum} {p.StepLabel}",
            $"IF {activation} THEN",
            $"    S{p.StepNum}_Exec := TRUE;",
            "END_IF;",
            $"IF {feedback} THEN",
            $"    S{p.StepNum}_Done := TRUE;",
            "END_IF;"
        };
    }

    public IList<string> RenderCleanupBlock(string triggerVar, IList<string> allVars)
    {
        var lines = new List<string> { $"IF {triggerVar} THEN" };
        lines.AddRange(allVars.Select(v => $"    {v} := FALSE;"));
        lines.Add("END_IF;");
        return lines;
    }

    private static string? NormalizeCondition(string? condition)
        => string.IsNullOrWhiteSpace(condition) || condition == "1" || condition.Equals("true", StringComparison.OrdinalIgnoreCase)
            ? null
            : condition;
}
