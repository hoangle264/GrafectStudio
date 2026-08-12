using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GrafcetStudio.Domain.Models;

namespace GrafcetStudio.App.Generators.Robot;

public class RobotSnippetPromptBuilder
{
    private readonly RobotRagLoader _ragLoader;

    public RobotSnippetPromptBuilder(RobotRagLoader? ragLoader = null)
    {
        _ragLoader = ragLoader ?? new RobotRagLoader();
    }

    public (string SystemPrompt, string UserPrompt) BuildPrompts(
        FlowInfo flow,
        IList<DeviceVariable> variables,
        TopologyContext topologyContext,
        string moduleName = "main")
    {
        var ragContent = _ragLoader.LoadRagContent("abb");
        var systemPrompt = BuildSystemPrompt(ragContent);
        var userPrompt = BuildUserPrompt(flow, variables, topologyContext, moduleName);

        return (systemPrompt, userPrompt);
    }

    private static string BuildSystemPrompt(string ragContent)
    {
        var sb = new StringBuilder();

        sb.AppendLine("You are an expert ABB RAPID Industrial Robot Code Generation Engine.");
        sb.AppendLine("Your job is to generate a JSON snippet map conforming to schema 'grafectstudio/robot-snippet-map/v1'.");
        sb.AppendLine();
        sb.AppendLine("### SCHEMA SPECIFICATION:");
        sb.AppendLine("Respond strictly with a JSON object containing:");
        sb.AppendLine("  - \"schema\": \"grafectstudio/robot-snippet-map/v1\"");
        sb.AppendLine("  - \"platform\": \"ABB_RAPID\"");
        sb.AppendLine("  - \"module\": string (module/flow name)");
        sb.AppendLine("  - \"signals\": array of { \"name\": string, \"type\": string, \"alias\": string }");
        sb.AppendLine("  - \"positions\": array of { \"name\": string, \"motionType\": string, \"speed\": string, \"description\": string }");
        sb.AppendLine("  - \"tools\": array of { \"name\": string, \"description\": string }");
        sb.AppendLine("  - \"init\": string (RAPID initialization statements, e.g. WaitTime. DO NOT declare VAR variables here!)");
        sb.AppendLine("  - \"steps\": object mapping step numbers (as keys or string numbers) to raw RAPID snippet text");
        sb.AppendLine("  - \"labels\": string (RAPID error / emergency stop handler statements)");
        sb.AppendLine();
        sb.AppendLine("### STRICT GENERATION & TRANSITION RULES:");
        sb.AppendLine("1. VARIABLE SAFETY: Only use variables present in the provided Variable Table. Never invent new variables.");
        sb.AppendLine("   Exception — RAPID built-in identifiers are pre-declared by the runtime and are EXEMPT from this rule:");
        sb.AppendLine("     • Speed data:  v5, v10, v20, v50, v100, v200, v300, v500, v1000, v1500, v2000, v3000, v4000, v5000, v6000, v7000");
        sb.AppendLine("     • Zone data:   fine, z0, z1, z5, z10, z15, z20, z30, z40, z50, z60, z80, z100, z150, z200");
        sb.AppendLine("     • Tool data:   tool0 (and any tooldata declared in system configuration)");
        sb.AppendLine("     • Sync ident:  syncident (used with WaitSyncTask)");
        sb.AppendLine("   These identifiers must NOT appear in the 'signals', 'positions', or 'tools' arrays of the output JSON.");
        sb.AppendLine("2. MACRO STEPS: Steps marked [MACRO CALL / Kind=macro] MUST NOT have an entry in the \"steps\" map. The engine will inject procedure calls automatically (`CallProc SubFlow;`).");
        sb.AppendLine("3. TRANSITION CONDITIONS:");
        sb.AppendLine("   - The engine AUTOMATICALLY injects the correct Wait instruction (WaitDI/WaitDO/WaitUntil) at the end of each step based on the transition condition.");
        sb.AppendLine("   - DO NOT generate ANY wait instructions for transitions in your step snippets.");
        sb.AppendLine("   - DO NOT generate `IF/ELSIF/TEST/CASE/GOTO` branching logic for transitions.");
        sb.AppendLine("   - Simply output the core actions (e.g., `MoveL`, `SetDO`) for the step and nothing else.");
        sb.AppendLine("4. BACKWARD JUMP TRANSITION (LOOP BACK: Step N -> Step M, M <= N):");
        sb.AppendLine("   - Same as Rule 3: The engine automatically injects the Wait instruction and `GOTO lbl_stepM;` after your snippet.");
        sb.AppendLine("   - DO NOT generate Wait or GOTO in your snippet.");
        sb.AppendLine("5. FORWARD JUMP & SELECTION BRANCHING:");
        sb.AppendLine("   - The engine AUTOMATICALLY generates the `IF/ELSIF/ELSE` and `TEST/CASE` blocks for selection branches and forward jumps.");
        sb.AppendLine("   - DO NOT generate `IF`, `ELSIF`, `ELSE`, `TEST`, `CASE`, or `GOTO` statements in your snippets for these transitions.");
        sb.AppendLine("   - Simply output the actions (e.g. MoveL, SetDO) for the step. The engine will wrap them in the correct branching structure.");
        sb.AppendLine("6. MERGE POINTS & CONVERGENCE:");
        sb.AppendLine("   - The engine automatically closes `IF/TEST` blocks and handles convergence transitions.");
        sb.AppendLine("   - DO NOT generate `END_IF` or `ENDTEST` statements. Simply output the actions for the step.");
        sb.AppendLine("7. RAPID SYNTAX:");
        sb.AppendLine("   - Signals: `SetDO signalName, 1;` or `WaitDI signalName, 1;`");
        sb.AppendLine("   - Motion: `MoveJ posName, v500, fine, tool0;` or `MoveL posName, v1000, z10, tool0;`");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(ragContent))
        {
            sb.AppendLine("### SUPPLEMENTAL KNOWLEDGE (RAG / RULES):");
            sb.AppendLine(ragContent);
            sb.AppendLine();
        }

        sb.AppendLine("### OUTPUT REQUIREMENTS:");
        sb.AppendLine("Return ONLY raw valid JSON (no markdown formatting, no commentary).");

        return sb.ToString();
    }

    private static string BuildUserPrompt(
        FlowInfo flow,
        IList<DeviceVariable> variables,
        TopologyContext topologyContext,
        string moduleName)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"Module Name: {moduleName}");
        sb.AppendLine();

        // Collect all referenced variable names from steps, transitions, and flow context
        var referencedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (flow?.Steps != null)
        {
            foreach (var step in flow.Steps)
            {
                if (step.Actions != null)
                {
                    foreach (var act in step.Actions)
                    {
                        if (!string.IsNullOrWhiteSpace(act.Variable))
                            referencedNames.Add(act.Variable.Trim());
                        if (!string.IsNullOrWhiteSpace(act.TargetAddress))
                            referencedNames.Add(act.TargetAddress.Trim());
                    }
                }
            }
        }

        if (flow?.Transitions != null)
        {
            foreach (var trans in flow.Transitions)
            {
                if (!string.IsNullOrWhiteSpace(trans.Condition))
                {
                    var matches = System.Text.RegularExpressions.Regex.Matches(trans.Condition, @"\b[a-zA-Z_][a-zA-Z0-9_]*\b");
                    foreach (System.Text.RegularExpressions.Match m in matches)
                    {
                        referencedNames.Add(m.Value);
                    }
                }
            }
        }

        var usedVariables = (variables != null && variables.Count > 0)
            ? variables.Where(v => !string.IsNullOrWhiteSpace(v.Label) && referencedNames.Contains(v.Label.Trim())).ToList()
            : new List<DeviceVariable>();

        if (usedVariables.Count == 0 && variables != null && variables.Count > 0)
        {
            usedVariables = variables.ToList();
        }

        // 1. Variable Table
        sb.AppendLine("=== VARIABLE TABLE ===");
        if (usedVariables.Count > 0)
        {
            sb.AppendLine("Label | Format | Address");
            foreach (var v in usedVariables)
            {
                sb.AppendLine($"{v.Label} | {v.Format} | {v.Address}");
            }
        }
        else
        {
            sb.AppendLine("No variables declared.");
        }
        sb.AppendLine();

        // 2. Grafcet Topology
        sb.AppendLine("=== GRAFCET TOPOLOGY ===");
        var steps = flow?.Steps ?? new List<Step>();
        var transitions = flow?.Transitions ?? new List<Transition>();

        var stepMap = steps.ToDictionary(s => s.Id, s => s);

        foreach (var step in steps.OrderBy(s => s.Number))
        {
            var isMacro = string.Equals(step.Kind, "macro", StringComparison.OrdinalIgnoreCase);
            var macroTarget = topologyContext.MacroCallMap.GetValueOrDefault(step.Number);

            sb.Append($"Step {step.Number} [{step.Label}] ");
            if (step.IsInitial) sb.Append("(INITIAL STEP) ");
            if (isMacro) sb.Append($"(Kind=macro → CallProc: {macroTarget}) ");
            else sb.Append("(normal) ");

            if (topologyContext.SelectionBranchMap.TryGetValue(step.Number, out var branchInfo))
            {
                var branchType = branchInfo.IsIntegerVariablePattern ? "TEST/ENDTEST" : "IF/ELSIF";
                sb.Append($"[SELECTION BRANCH — {branchType}] ");
            }

            sb.AppendLine(":");

            if (step.Actions != null && step.Actions.Count > 0)
            {
                sb.AppendLine("  Actions:");
                foreach (var act in step.Actions)
                {
                    var qualStr = act.Qualifier.ToString();
                    var showQualifier = !string.IsNullOrWhiteSpace(qualStr) && !string.Equals(qualStr, "N", StringComparison.OrdinalIgnoreCase);
                    var qualifierStr = showQualifier ? $"Qualifier: {qualStr}, " : string.Empty;
                    sb.AppendLine($"    - {qualifierStr}Variable: {act.Variable}, Address: {act.TargetAddress}, TimeMs: {act.TimeMs}");
                }
            }

            if (isMacro)
            {
                sb.AppendLine("  [DO NOT generate snippet in \"steps\" — SkeletonEngine will inject CallProc]");
            }

            // Transitions from this step
            var outgoingDetails = topologyContext.TransitionDetails
                .Where(td => td.FromStepNumber == step.Number)
                .ToList();

            if (outgoingDetails.Count > 0)
            {
                sb.AppendLine("  Transitions:");
                foreach (var td in outgoingDetails)
                {
                    var condStr = string.IsNullOrWhiteSpace(td.Condition) ? "always" : td.Condition;

                    switch (td.Kind)
                    {
                        case TransitionKind.SequentialWait:
                            sb.AppendLine($"    → Step {td.ToStepNumber} (SEQUENTIAL 1-STEP: condition = \"{condStr}\") [Engine auto-injects transition Wait. DO NOT generate Wait/WaitDI/WaitUntil in snippet]");
                            break;
                        case TransitionKind.ForwardJump:
                            sb.AppendLine($"    → Step {td.ToStepNumber} (FORWARD JUMP > 1 STEP: condition = \"{condStr}\") [Engine auto-injects IF/ELSE structure. DO NOT generate branching logic]");
                            break;
                        case TransitionKind.BackwardJump:
                            sb.AppendLine($"    → Step {td.ToStepNumber} (BACKWARD JUMP: condition = \"{condStr}\") ⬆ [DO NOT generate Wait or GOTO; engine auto-injects backward loop transition]");
                            break;
                        case TransitionKind.SelectionBranch:
                            sb.AppendLine($"    → Step {td.ToStepNumber} (SELECTION BRANCH ARM: condition = \"{condStr}\") [Engine auto-injects TEST/CASE or IF/ELSIF. DO NOT generate branching logic]");
                            break;
                        case TransitionKind.Convergence:
                            sb.AppendLine($"    → Step {td.ToStepNumber} (CONVERGENCE: condition = \"{condStr}\") [Engine auto-closes branch blocks. DO NOT generate END_IF or ENDTEST]");
                            break;
                    }
                }
            }

            sb.AppendLine();
        }

        sb.AppendLine("=== INSTRUCTIONS ===");
        sb.AppendLine($"Generate the JSON snippet map for module '{moduleName}'.");
        sb.AppendLine("Ensure max nesting depth <= 4 levels (IF/ELSIF + TEST/CASE combined).");
        sb.AppendLine("Ensure all declared positions, signals, tools are listed in 'signals', 'positions', 'tools'.");

        return sb.ToString();
    }
}
