using System.Text.RegularExpressions;

namespace GrafcetStudio.App.Generators.Robot;

/// <summary>
/// Fail-closed static check of a complete RAPID module returned by the AI:
/// block balance, module/PROC main presence, label/GOTO resolution, unknown identifiers,
/// and that every instruction the structure plan requires (waits, branches, jumps, macro calls) is present.
/// </summary>
public static partial class RapidModuleValidator
{
    private static readonly HashSet<string> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "MODULE", "ENDMODULE", "PROC", "ENDPROC", "FUNC", "ENDFUNC", "TRAP", "ENDTRAP",
        "IF", "THEN", "ELSIF", "ELSE", "ENDIF", "END_IF", "WHILE", "DO", "ENDWHILE",
        "FOR", "FROM", "TO", "STEP", "ENDFOR", "TEST", "CASE", "DEFAULT", "ENDTEST",
        "GOTO", "RETURN", "ERROR", "UNDO", "BACKWARD", "VAR", "PERS", "CONST", "LOCAL", "TASK",
        "AND", "OR", "XOR", "NOT", "DIV", "MOD", "TRUE", "FALSE"
    };

    /// <summary>RAPID instructions, functions, data types and pre-declared data that need no declaration.</summary>
    private static readonly HashSet<string> BuiltIns = new(StringComparer.OrdinalIgnoreCase)
    {
        // motion
        "MoveJ", "MoveL", "MoveC", "MoveAbsJ", "MoveJDO", "MoveLDO", "MoveCDO", "SearchL", "StopMove", "StartMove",
        "ConfJ", "ConfL", "SingArea", "AccSet", "VelSet", "Offs", "RelTool", "CRobT", "CJointT",
        // IO and waits
        "SetDO", "Set", "Reset", "PulseDO", "SetAO", "SetGO", "InvertDO", "WaitDI", "WaitDO", "WaitAI", "WaitAO",
        "WaitGI", "WaitGO", "WaitUntil", "WaitTime", "WaitSyncTask", "DInput", "DOutput", "AInput", "GInput",
        // flow and messages
        "Stop", "EXIT", "Break", "RETRY", "TRYNEXT", "RAISE", "TPWrite", "TPErase", "TPReadFK", "ErrWrite",
        "ClkStart", "ClkStop", "ClkReset", "ClkRead", "Abs", "Round", "Trunc", "NumToStr", "ValToStr", "StrLen",
        "ERRNO", "ERR_WAIT_MAXTIME", "MaxTime", "TimeFlag", "InPos", "Conc",
        // data types
        "num", "dnum", "bool", "string", "byte", "robtarget", "jointtarget", "tooldata", "wobjdata", "loaddata",
        "speeddata", "zonedata", "signaldi", "signaldo", "signalai", "signalao", "signalgi", "signalgo",
        "intnum", "clock", "syncident", "tasks", "pos", "orient", "pose", "confdata", "robjoint", "extjoint", "errnum",
        // pre-declared data
        "tool0", "wobj0", "load0", "vmax", "fine", "high", "low",
        "v5", "v10", "v20", "v30", "v40", "v50", "v60", "v80", "v100", "v150", "v200", "v300", "v400", "v500",
        "v600", "v800", "v1000", "v1500", "v2000", "v2500", "v3000", "v4000", "v5000", "v6000", "v7000",
        "z0", "z1", "z5", "z10", "z15", "z20", "z30", "z40", "z50", "z60", "z80", "z100", "z150", "z200",
        // module attributes
        "SYSMODULE", "NOVIEW", "NOSTEPIN", "VIEWONLY", "READONLY"
    };

    [GeneratedRegex(@"\d[\d.]*(?:[eE][+-]?\d+)?|[A-Za-z_][A-Za-z0-9_]*|:=|[:;\\.]")]
    private static partial Regex TokenPattern();

    private sealed record Tok(string Value, int Line, char Before);

    public static IReadOnlyList<string> Validate(
        string code,
        string expectedModuleName,
        IEnumerable<string> declaredVariables,
        IEnumerable<string> requiredFragments)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(code)) return new[] { "AI returned no code." };

        var tokens = Tokenize(code);
        CheckBlocks(tokens, expectedModuleName, errors);

        var declared = CollectDeclarations(tokens, out var labels, out var gotoTargets);
        foreach (var target in gotoTargets.Where(t => !labels.Contains(t.Value)))
            errors.Add($"Line {target.Line}: GOTO target '{target.Value}' has no label.");

        var allowed = new HashSet<string>(declaredVariables.Where(v => !string.IsNullOrWhiteSpace(v)), StringComparer.OrdinalIgnoreCase);
        var unknown = tokens
            .Where(t => IsIdentifier(t.Value) && t.Before != '.' && t.Before != '\\')
            .Where(t => !Keywords.Contains(t.Value) && !BuiltIns.Contains(t.Value) && !declared.Contains(t.Value) && !allowed.Contains(t.Value) && !labels.Contains(t.Value))
            .GroupBy(t => t.Value, StringComparer.OrdinalIgnoreCase)
            .Take(20);
        foreach (var group in unknown)
            errors.Add($"Line {group.First().Line}: unknown identifier '{group.Key}' (not in the Variable Table, not declared in the module, not a RAPID built-in).");

        var normalizedCode = Normalize(code);
        foreach (var fragment in requiredFragments.Distinct())
        {
            if (!normalizedCode.Contains(Normalize(fragment), StringComparison.Ordinal))
                errors.Add($"Required instruction from the structure plan is missing: {fragment}");
        }

        return errors;
    }

    /// <summary>Strips markdown fences and anything outside MODULE ... ENDMODULE.</summary>
    public static string ExtractModule(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText)) return string.Empty;
        var start = Regex.Match(rawText, @"^\s*MODULE\b", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        var end = Regex.Matches(rawText, @"\bENDMODULE\b", RegexOptions.IgnoreCase).LastOrDefault();
        if (!start.Success || end is null || end.Index < start.Index) return rawText.Trim();
        return rawText.Substring(start.Index, end.Index + end.Length - start.Index).Trim();
    }

    private static List<Tok> Tokenize(string code)
    {
        var tokens = new List<Tok>();
        var lines = code.Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = StripCommentsAndStrings(lines[i]);
            foreach (Match m in TokenPattern().Matches(line))
            {
                var before = m.Index > 0 ? line[..m.Index].TrimEnd().LastOrDefault() : '\0';
                tokens.Add(new Tok(m.Value, i + 1, before));
            }
        }
        return tokens;
    }

    private static string StripCommentsAndStrings(string line)
    {
        var chars = line.ToCharArray();
        var inString = false;
        for (var i = 0; i < chars.Length; i++)
        {
            if (chars[i] == '"') { inString = !inString; chars[i] = ' '; continue; }
            if (inString) { chars[i] = ' '; continue; }
            if (chars[i] == '!') return new string(chars, 0, i);
        }
        return new string(chars);
    }

    private static void CheckBlocks(List<Tok> tokens, string expectedModuleName, List<string> errors)
    {
        var stack = new Stack<Tok>();
        var moduleCount = 0;
        var hasMain = false;

        for (var i = 0; i < tokens.Count; i++)
        {
            var t = tokens[i];
            var v = t.Value.ToUpperInvariant();
            switch (v)
            {
                case "MODULE":
                    moduleCount++;
                    var name = i + 1 < tokens.Count ? tokens[i + 1].Value : string.Empty;
                    if (!string.Equals(name, expectedModuleName, StringComparison.OrdinalIgnoreCase))
                        errors.Add($"Line {t.Line}: module must be named '{expectedModuleName}' (found '{name}').");
                    stack.Push(t);
                    break;
                case "PROC":
                    if (i + 1 < tokens.Count && string.Equals(tokens[i + 1].Value, "main", StringComparison.OrdinalIgnoreCase)) hasMain = true;
                    stack.Push(t);
                    break;
                case "FUNC" or "TRAP" or "WHILE" or "FOR" or "TEST":
                    stack.Push(t);
                    break;
                case "IF":
                    if (IsBlockIf(tokens, i)) stack.Push(t);
                    break;
                case "ELSIF" or "ELSE":
                    if (stack.Count == 0 || !stack.Peek().Value.Equals("IF", StringComparison.OrdinalIgnoreCase))
                        errors.Add($"Line {t.Line}: {v} without a matching IF ... THEN.");
                    break;
                case "CASE" or "DEFAULT":
                    if (stack.Count == 0 || !stack.Peek().Value.Equals("TEST", StringComparison.OrdinalIgnoreCase))
                        errors.Add($"Line {t.Line}: {v} outside a TEST block.");
                    break;
                case "END_IF":
                    errors.Add($"Line {t.Line}: END_IF is not RAPID syntax; use ENDIF.");
                    Close(stack, "IF", t, errors);
                    break;
                case "ENDIF": Close(stack, "IF", t, errors); break;
                case "ENDWHILE": Close(stack, "WHILE", t, errors); break;
                case "ENDFOR": Close(stack, "FOR", t, errors); break;
                case "ENDTEST": Close(stack, "TEST", t, errors); break;
                case "ENDPROC": Close(stack, "PROC", t, errors); break;
                case "ENDFUNC": Close(stack, "FUNC", t, errors); break;
                case "ENDTRAP": Close(stack, "TRAP", t, errors); break;
                case "ENDMODULE": Close(stack, "MODULE", t, errors); break;
            }
        }

        foreach (var open in stack)
            errors.Add($"Line {open.Line}: {open.Value.ToUpperInvariant()} is never closed.");
        if (moduleCount != 1) errors.Add($"Code must contain exactly one MODULE (found {moduleCount}).");
        if (!hasMain) errors.Add("Module must contain PROC main().");
    }

    private static bool IsBlockIf(List<Tok> tokens, int ifIndex)
    {
        for (var j = ifIndex + 1; j < tokens.Count; j++)
        {
            if (tokens[j].Value == ";") return false;
            if (tokens[j].Value.Equals("THEN", StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static void Close(Stack<Tok> stack, string opener, Tok closer, List<string> errors)
    {
        if (stack.Count > 0 && stack.Peek().Value.Equals(opener, StringComparison.OrdinalIgnoreCase))
        {
            stack.Pop();
            return;
        }
        var found = stack.Count > 0 ? stack.Peek().Value.ToUpperInvariant() + " (line " + stack.Peek().Line + ")" : "nothing";
        errors.Add($"Line {closer.Line}: {closer.Value.ToUpperInvariant()} does not close {opener}; innermost open block is {found}.");
    }

    private static HashSet<string> CollectDeclarations(List<Tok> tokens, out HashSet<string> labels, out List<Tok> gotoTargets)
    {
        var declared = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        gotoTargets = new List<Tok>();

        for (var i = 0; i < tokens.Count; i++)
        {
            var v = tokens[i].Value.ToUpperInvariant();
            if (v is "VAR" or "PERS" or "CONST")
            {
                // VAR <type> <name>
                if (i + 2 < tokens.Count && IsIdentifier(tokens[i + 2].Value)) declared.Add(tokens[i + 2].Value);
            }
            else if (v is "MODULE" or "PROC" or "FUNC" or "TRAP")
            {
                // FUNC <type> <name>, otherwise <keyword> <name>
                var nameIndex = v == "FUNC" ? i + 2 : i + 1;
                if (nameIndex < tokens.Count && IsIdentifier(tokens[nameIndex].Value)) declared.Add(tokens[nameIndex].Value);
            }
            else if (v == "GOTO" && i + 1 < tokens.Count)
            {
                gotoTargets.Add(tokens[i + 1]);
            }
            else if (IsIdentifier(tokens[i].Value) && i + 1 < tokens.Count && tokens[i + 1].Value == ":" && !Keywords.Contains(tokens[i].Value))
            {
                // "CASE 1:" yields a number before ':', so only identifiers become labels.
                labels.Add(tokens[i].Value);
            }
        }

        return declared;
    }

    private static bool IsIdentifier(string value) => value.Length > 0 && (char.IsLetter(value[0]) || value[0] == '_');

    private static string Normalize(string text)
    {
        var withoutComments = string.Join("\n", text.Replace("\r\n", "\n").Split('\n').Select(StripCommentsKeepStrings));
        return Regex.Replace(withoutComments, @"\s+", string.Empty).ToLowerInvariant();
    }

    private static string StripCommentsKeepStrings(string line)
    {
        var inString = false;
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '"') inString = !inString;
            else if (!inString && line[i] == '!') return line[..i];
        }
        return line;
    }
}
