using System;
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

        for (var i = 0; i < sequence.Count; i++)
        {
            var entry = sequence[i];
            var exec = RequireStepAddress(entry.Step.ExecAddress, entry.Step, "execAddress");
            var done = RequireStepAddress(entry.Step.DoneAddress, entry.Step, "doneAddress");
            var prev = entry.Step.IsInitial || i == 0
                ? new List<string> { "TRUE" }
                : new List<string> { RequireStepAddress(sequence[i - 1].Step.DoneAddress, sequence[i - 1].Step, "doneAddress") };
            lines.AddRange(RenderStepLogic(new StStepRenderParams
            {
                StepNum = entry.Step.Number.ToString("D2"),
                ExecAddress = exec,
                DoneAddress = done,
                PrevDoneVars = prev,
                ActivationCond = NormalizeCondition(entry.InTransition?.Condition),
                FeedbackCond = NormalizeCondition(entry.OutTransition?.Condition),
                StepLabel = entry.Step.Label
            }));

            foreach (var action in entry.Step.Actions.Where(a => a.Qualifier == Domain.Enums.ActionQualifier.N))
            {
                lines.Add($"{action.ToPhysicalAddress(state.Variables)} := {exec};");
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
        var feedback = string.IsNullOrWhiteSpace(p.FeedbackCond) ? p.ExecAddress : $"{p.ExecAddress} AND ({p.FeedbackCond})";

        return new List<string>
        {
            $"// S{p.StepNum} {p.StepLabel}",
            $"IF {activation} THEN",
            $"    {p.ExecAddress} := TRUE;",
            "END_IF;",
            $"IF {feedback} THEN",
            $"    {p.DoneAddress} := TRUE;",
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

    private static string RequireStepAddress(string? address, Step step, string propertyName)
        => string.IsNullOrWhiteSpace(address)
            ? throw new InvalidOperationException($"Invalid codegen payload: step '{step.Id}' (S{step.Number:D2}) is missing {propertyName}.")
            : address;

    private static string? NormalizeCondition(string? condition)
        => string.IsNullOrWhiteSpace(condition) || condition == "1" || condition.Equals("true", StringComparison.OrdinalIgnoreCase)
            ? null
            : condition;
}
