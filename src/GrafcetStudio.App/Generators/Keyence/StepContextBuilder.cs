using GrafcetStudio.CodeGen.Runtime.Models;
using GrafcetStudio.Domain.Models;
using GrafcetStudio.Domain.Resolution;
using System.Collections.Generic;
using System.Linq;

namespace GrafcetStudio.App.Generators.Keyence;

/// <summary>
/// Utility helpers used by PLC code generators.
/// Expression/mnemonic building is intentionally removed — templates now use
/// the pseudo-expression syntax (A &amp; B -&gt; INST target) which is post-processed
/// by <see cref="MnemonicEmitter.ConvertPseudoExpressions"/>.
/// </summary>
internal static class StepContextBuilder
{
    /// <summary>
    /// Formats a combined instruction expression: "condition -> INST target" or "INST target".
    /// Returns empty if instruction or target is blank.
    /// </summary>
    public static string BuildInstructionExpression(string conditionExpression, string instruction, string target)
    {
        if (string.IsNullOrWhiteSpace(instruction) || string.IsNullOrWhiteSpace(target)) return string.Empty;

        return string.IsNullOrWhiteSpace(conditionExpression)
            ? $"{instruction} {target}"
            : $"{conditionExpression} -> {instruction} {target}";
    }

    /// <summary>
    /// Maps an IEC action qualifier string to a Keyence instruction mnemonic.
    /// </summary>
    public static string ResolveActionInstruction(string qualifier)
        => qualifier.ToUpperInvariant() switch
        {
            "S" or "SD" or "SL" => "SET",
            "R" => "RST",
            _ => "OUT"
        };

    /// <summary>
    /// Resolves the physical address target for an action (direct address > variable lookup > variable name).
    /// </summary>
    public static string ResolveActionTarget(StepAction action, IList<DeviceVariable> variables)
    {
        if (!string.IsNullOrWhiteSpace(action.Address)) return action.Address!;

        var resolved = SignalResolver.ResolveAddress(action.Variable, variables);
        return string.IsNullOrWhiteSpace(resolved) ? action.Variable : resolved!;
    }
}
