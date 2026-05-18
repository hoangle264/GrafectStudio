using System.Collections.Generic;
using System.Text;
using GrafcetStudio.CodeGen.Profile;
using GrafcetStudio.CodeGen.Models;
using GrafcetStudio.Domain.Models;
using GrafcetStudio.Domain.Resolution;

namespace GrafcetStudio.CodeGen;

/// <summary>Generates IL code for configured diagrams and output bindings.</summary>
public class IlGenerator : ICodeGenerator
{
    private readonly IProjectRepository _repo;
    private readonly ISequenceResolver _sequenceResolver;

    public IlGenerator(IProjectRepository repo, ISequenceResolver sequenceResolver)
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

        var code = string.Join(Environment.NewLine, lines);
        return new GenerationResult
        {
            Code = ProfileRegistry.ApplyProfile(code, opts.Profile),
            Stats = $"Diagrams: {diagIds.Count}, Steps: {totalSteps}",
            Diagnostics = diagnostics
        };
    }

    public DiagramResult GenerateDiagram(DiagramMeta meta, DiagramState state, GenerationOptions opts)
    {
        var sequence = _sequenceResolver.Resolve(state);
        var mrMap = opts.MrMap ?? _sequenceResolver.AllocateMrMap(sequence, opts.BaseMr);
        var lines = new List<string> { $"{opts.Profile.CommentPrefix} Diagram: {meta.Name} ({meta.Mode})" };

        foreach (var entry in sequence)
        {
            if (!mrMap.TryGetValue(entry.Step.Id, out var mr)) continue;
            lines.Add(BuildStepComment(entry.Step.Number, entry.Step.Label));
            lines.AddRange(BuildStepActivation(entry, mr, opts));
            lines.AddRange(BuildStepFeedback(entry, mr, opts));

            if (!opts.SeparateOutputs)
            {
                foreach (var action in entry.Step.Actions)
                {
                    lines.Add($"LD   {PadAddress(mr.Exec)}{opts.Profile.CommentPrefix} S{entry.Step.Number:D2} action");
                    lines.Add($"{Map(action.Qualifier is Domain.Enums.ActionQualifier.S ? "SET" : action.Qualifier is Domain.Enums.ActionQualifier.R ? "RST" : "OUT"),-5}{action.ToPhysicalAddress(state.Variables)}");
                }
            }

            lines.Add(string.Empty);
        }

        return new DiagramResult { Lines = lines, StepCount = sequence.Count };
    }

    public IList<string> GenerateOutputSection(IList<DiagramEntry> entries, IDictionary<string, IList<SignalActionEntry>> signalActionMap)
        => signalActionMap.SelectMany(pair => new[] { $"LD   {BuildExecMrBlock(pair.Value)}", $"OUT  {pair.Key}" }).ToList();

    private string BuildExecMrBlock(IList<SignalActionEntry> actions)
        => string.Join($" {Map("OR")} ", actions.Select(a => a.ExecMr));

    private string BuildPrevStepBlock(IList<Step> prevSteps, IDictionary<string, MrPair> mrMap)
        => string.Join($" {Map("OR")} ", prevSteps.Where(s => mrMap.ContainsKey(s.Id)).Select(s => mrMap[s.Id].Done));

    private string ApplyOutputTemplate(string template, IDictionary<string, string> vars)
    {
        foreach (var pair in vars) template = template.Replace("{{" + pair.Key + "}}", pair.Value);
        return template;
    }

    private IList<string> BuildStepActivation(SequenceEntry entry, MrPair mr, GenerationOptions opts)
    {
        var prev = entry.Step.IsInitial ? "CR2002" : entry.InTransition is null ? "CR2002" : mr.Done;
        var lines = new List<string> { $"LD   {PadAddress(prev)}{opts.Profile.CommentPrefix} previous done" };
        if (entry.InTransition is { Condition: not "1" } t && !string.IsNullOrWhiteSpace(t.Condition))
        {
            lines.Add($"{Map("AND"),-5}{t.Condition}");
        }

        lines.Add($"{Map("SET"),-5}{mr.Exec}");
        return lines;
    }

    private IList<string> BuildStepFeedback(SequenceEntry entry, MrPair mr, GenerationOptions opts)
    {
        var lines = new List<string> { $"LD   {PadAddress(mr.Exec)}{opts.Profile.CommentPrefix} step exec" };
        if (entry.OutTransition is { Condition: not "1" } t && !string.IsNullOrWhiteSpace(t.Condition))
        {
            lines.Add($"{Map("AND"),-5}{t.Condition}");
        }

        lines.Add($"{Map("SET"),-5}{mr.Done}");
        return lines;
    }

    private string BuildStepComment(int stepNum, string stepLabel)
        => $"; S{stepNum:D2} {stepLabel}";

    private string PadAddress(string addr) => addr.PadRight(12);

    private string Map(string instruction)
        => ProfileRegistry.Kv8000.InstructionMap.TryGetValue(instruction, out var mapped) ? mapped : instruction;
}
