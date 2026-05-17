using GrafcetStudio.App.Models;
using System;
using System.Text;

namespace GrafcetStudio.App.Generators;

public class TwinCatStGenerator : ICodeGenerator
{
    public string Platform => "twincat-st";

    public string Generate(CodegenPayload payload)
    {
        var seq = SequenceBuilder.Build(payload);
        var sb = new StringBuilder();
        sb.AppendLine("// TwinCAT ST stub");

        foreach (var item in seq)
        {
            var n = item.Step.Number;
            var exec = $"s{n:D2}_exec";
            var done = $"s{n:D2}_done";
            sb.AppendLine($"VAR {exec} : BOOL; {done} : BOOL; END_VAR");

            var prev = item.Step.Initial ? "TRUE" : $"s{Math.Max(1, n - 1):D2}_done";
            var inCond = NormalizeCondition(item.InTransition?.Condition, payload);
            sb.AppendLine($"IF {prev} AND {inCond} THEN {exec} := TRUE; END_IF;");

            var outCond = NormalizeCondition(item.OutTransition?.Condition, payload);
            sb.AppendLine($"IF {exec} AND {outCond} THEN {done} := TRUE; {exec} := FALSE; END_IF;");

            foreach (var action in item.Step.Actions)
            {
                var addr = !string.IsNullOrWhiteSpace(action.Address)
                    ? action.Address!
                    : AddressResolver.Resolve(action.Variable, payload.Variables);
                if (string.Equals(action.Qualifier, "N", StringComparison.OrdinalIgnoreCase))
                    sb.AppendLine($"{addr} := {exec};");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string NormalizeCondition(string? condition, CodegenPayload payload)
    {
        if (string.IsNullOrWhiteSpace(condition) || condition == "1" || condition.Equals("true", StringComparison.OrdinalIgnoreCase))
            return "TRUE";
        return AddressResolver.Resolve(condition, payload.Variables);
    }
}
