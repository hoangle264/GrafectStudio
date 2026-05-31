using System;
using System.Text;
using GrafcetStudio.Domain.Enums;
using GrafcetStudio.Domain.Models;

namespace GrafcetStudio.App.Generators;

public class TwinCatStGenerator : ICodeGenerator
{
    public string Platform => "twincat-st";

    public string Generate(CodegenPayload payload)
    {
        var seq = SequenceBuilder.Build(payload);
        var sb = new StringBuilder();
        sb.AppendLine("// TwinCAT ST stub");

        for (var i = 0; i < seq.Count; i++)
        {
            var item = seq[i];
            var n = item.Step.Number;
            var exec = RequireStepAddress(item.Step.ExecAddress, item.Step, "execAddress");
            var done = RequireStepAddress(item.Step.DoneAddress, item.Step, "doneAddress");
            sb.AppendLine($"// S{n:D2}: exec={exec}, done={done}");

            var prev = item.Step.IsInitial || i == 0
                ? "TRUE"
                : RequireStepAddress(seq[i - 1].Step.DoneAddress, seq[i - 1].Step, "doneAddress");
            var inCond = NormalizeCondition(item.InTransition?.Condition, payload);
            sb.AppendLine($"IF {prev} AND {inCond} THEN {exec} := TRUE; END_IF;");

            var outCond = NormalizeCondition(item.OutTransition?.Condition, payload);
            sb.AppendLine($"IF {exec} AND {outCond} THEN {done} := TRUE; {exec} := FALSE; END_IF;");

            foreach (var action in item.Step.Actions)
            {
                var addr = !string.IsNullOrWhiteSpace(action.Address)
                    ? action.Address!
                    : AddressResolver.Resolve(action.Variable, payload.Variables);
                if (action.Qualifier == ActionQualifier.N)
                    sb.AppendLine($"{addr} := {exec};");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string RequireStepAddress(string? address, Step step, string propertyName)
        => string.IsNullOrWhiteSpace(address)
            ? throw new InvalidOperationException($"Invalid codegen payload: step '{step.Id}' (S{step.Number:D2}) is missing {propertyName}.")
            : address;

    private static string NormalizeCondition(string? condition, CodegenPayload payload)
    {
        if (string.IsNullOrWhiteSpace(condition) || condition == "1" || condition.Equals("true", StringComparison.OrdinalIgnoreCase))
            return "TRUE";
        return AddressResolver.Resolve(condition, payload.Variables);
    }
}
