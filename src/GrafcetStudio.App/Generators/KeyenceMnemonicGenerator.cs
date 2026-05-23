using System;
using System.Collections.Generic;
using System.Text;
using GrafcetStudio.Domain.Enums;
using GrafcetStudio.Domain.Models;

namespace GrafcetStudio.App.Generators;

public class KeyenceMnemonicGenerator : ICodeGenerator
{
    public string Platform => "kv-5500";

    public string Generate(CodegenPayload payload)
    {
        var seq = SequenceBuilder.Build(payload);
        var sb = new StringBuilder();
        sb.AppendLine($"; Project: {payload.Project?.Name ?? "Unknown"}");
        sb.AppendLine($"; Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        var map = new List<string>();

        for (var i = 0; i < seq.Count; i++)
        {
            var item = seq[i];
            var exec = $"@MD{100 + i * 2}";
            var done = $"@MR{101 + i * 2}";
            map.Add($"; S{item.Step.Number:D2}: exec={exec}, done={done}");
            var prevDone = item.Step.IsInitial || i == 0 ? "CR2002" : $"@MR{101 + (i - 1) * 2}";

            sb.AppendLine($"LD   {prevDone.PadRight(12)}; S{item.Step.Number:D2} prev done");
            EmitConditionAndSet(sb, item.InTransition?.Condition, payload.Variables, exec, $"S{item.Step.Number:D2} exec");
            EmitActions(sb, item.Step, exec, payload.Variables);
            sb.AppendLine($"LD   {exec.PadRight(12)}; S{item.Step.Number:D2} exec");
            EmitConditionAndSet(sb, item.OutTransition?.Condition, payload.Variables, done, $"S{item.Step.Number:D2} done");
            sb.AppendLine();
        }

        foreach (var line in map) sb.AppendLine(line);
        return sb.ToString();
    }

    private static void EmitConditionAndSet(StringBuilder sb, string? condition, IReadOnlyList<DeviceVariable> vars, string target, string comment)
    {
        if (!SkipCondition(condition))
        {
            var resolved = AddressResolver.Resolve(condition!, vars);
            sb.AppendLine($"AND  {resolved}");
        }

        sb.AppendLine($"SET  {target.PadRight(12)}; {comment}");
    }

    private static void EmitActions(StringBuilder sb, Step step, string exec, IReadOnlyList<DeviceVariable> vars)
    {
        foreach (var action in step.Actions)
        {
            var addr = !string.IsNullOrWhiteSpace(action.Address) ? action.Address! : AddressResolver.Resolve(action.Variable, vars);
            sb.AppendLine($"LD   {exec.PadRight(12)}; S{step.Number:D2} exec");
            switch (action.Qualifier)
            {
                case ActionQualifier.N: sb.AppendLine($"OUT  {addr}"); break;
                case ActionQualifier.S: sb.AppendLine($"SET  {addr}"); break;
                case ActionQualifier.R: sb.AppendLine($"RST  {addr}"); break;
                default: sb.AppendLine($"; [{action.Qualifier}] {addr} - not implemented"); break;
            }
        }
    }

    private static bool SkipCondition(string? condition)
        => string.IsNullOrWhiteSpace(condition)
           || condition == "1"
           || condition.Equals("true", StringComparison.OrdinalIgnoreCase);
}
