using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GrafcetStudio.Domain.Models;

namespace GrafcetStudio.App.Generators.Robot;

public class RobotSkeletonEngine
{
    private readonly string _skeletonBasePath;

    public RobotSkeletonEngine(string? skeletonBasePath = null)
    {
        _skeletonBasePath = skeletonBasePath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "skeletons");
    }

    public string Render(SnippetMapDocument doc, TopologyContext topologyContext, FlowInfo flow, string moduleName = "main")
    {
        var skeletonText = LoadSkeleton("abb");

        var signalDecls = RenderSignalDeclarations(doc.Signals);
        var positionDecls = RenderPositionDeclarations(doc.Positions);
        var toolDecls = RenderToolDeclarations(doc.Tools);

        var initText = IndentText(doc.Init ?? "! Default initialization\nWaitTime 0.1;", 8);
        var stepsText = RenderSteps(doc, topologyContext, flow);
        var labelsText = IndentText(doc.Labels ?? "! Error / E-Stop handlers\nlbl_estop:\n    WaitTime 0.1;", 8);

        var result = skeletonText
            .Replace("{MODULE_NAME}", moduleName)
            .Replace("{DATE}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
            .Replace("{INJECT:signal_decls}", signalDecls)
            .Replace("{INJECT:position_decls}", positionDecls)
            .Replace("{INJECT:tool_decls}", toolDecls)
            .Replace("{INJECT:init}", initText)
            .Replace("{INJECT:steps}", stepsText)
            .Replace("{INJECT:labels}", labelsText);

        return result;
    }

    private string LoadSkeleton(string platform)
    {
        var skelFile = Path.Combine(_skeletonBasePath, platform.ToLowerInvariant(), "main.skel");
        if (File.Exists(skelFile))
        {
            return File.ReadAllText(skelFile);
        }

        // Fallback for execution from relative project dir
        var fallbackFile = Path.Combine(Directory.GetCurrentDirectory(), "assets", "skeletons", platform.ToLowerInvariant(), "main.skel");
        if (File.Exists(fallbackFile))
        {
            return File.ReadAllText(fallbackFile);
        }

        // Hardcoded default skeleton fallback
        return @"MODULE {MODULE_NAME}
    ! --- I/O Signal Aliases ---
{INJECT:signal_decls}

    ! --- Position Targets ---
{INJECT:position_decls}

    ! --- Tool Data ---
{INJECT:tool_decls}

    PROC main()
        ! === INITIALIZATION ===
{INJECT:init}

        ! === MAIN SEQUENCE LOOP ===
        WHILE TRUE DO
{INJECT:steps}
        ENDWHILE

        ! === ERROR / E-STOP HANDLERS ===
{INJECT:labels}
    ENDPROC
ENDMODULE";
    }

    private static string RenderSignalDeclarations(List<SnippetSignal> signals)
    {
        if (signals == null || signals.Count == 0) return "    ! No signals declared";
        var sb = new StringBuilder();
        foreach (var sig in signals)
        {
            var rapidSigType = sig.Type.ToLowerInvariant() switch
            {
                "do" or "digitaloutput" or "signaldo" => "signaldo",
                "di" or "digitalinput" or "signaldi" => "signaldi",
                "ao" or "analogoutput" or "signalao" => "signalao",
                "ai" or "analoginput" or "signalai" => "signalai",
                _ => "signaldo"
            };
            sb.AppendLine($"    VAR {rapidSigType} {sig.Name}; ! Alias: {sig.Alias}");
        }
        return sb.ToString().TrimEnd();
    }

    private static string RenderPositionDeclarations(List<SnippetPosition> positions)
    {
        if (positions == null || positions.Count == 0) return "    ! No position targets declared";
        var sb = new StringBuilder();
        foreach (var pos in positions)
        {
            sb.AppendLine($"    PERS robtarget {pos.Name} := [[0,0,0],[1,0,0,0],[0,0,0,0],[9E9,9E9,9E9,9E9,9E9,9E9]]; ! Motion: {pos.MotionType}, Speed: {pos.Speed}");
        }
        return sb.ToString().TrimEnd();
    }

    private static string RenderToolDeclarations(List<SnippetTool> tools)
    {
        if (tools == null || tools.Count == 0) return "    PERS tooldata tool0 := [TRUE,[[0,0,0],[1,0,0,0]],[1,[0,0,1],[1,0,0,0],0,0,0]];";
        var sb = new StringBuilder();
        foreach (var tool in tools)
        {
            if (string.Equals(tool.Name, "tool0", StringComparison.OrdinalIgnoreCase)) continue;
            sb.AppendLine($"    PERS tooldata {tool.Name} := [TRUE,[[0,0,0],[1,0,0,0]],[1,[0,0,1],[1,0,0,0],0,0,0]]; ! {tool.Description}");
        }
        if (sb.Length == 0) return "    PERS tooldata tool0 := [TRUE,[[0,0,0],[1,0,0,0]],[1,[0,0,1],[1,0,0,0],0,0,0]];";
        return sb.ToString().TrimEnd();
    }

    private static string RenderSteps(SnippetMapDocument doc, TopologyContext topologyContext, FlowInfo flow)
    {
        if (topologyContext?.ExecutionTree != null && topologyContext.ExecutionTree.Count > 0)
        {
            var sb = new StringBuilder();
            foreach (var block in topologyContext.ExecutionTree)
            {
                RenderExecutionBlock(sb, block, doc, topologyContext, 8);
            }
            return sb.ToString().TrimEnd();
        }

        // Fallback for legacy sequential steps
        var legacySb = new StringBuilder();
        var steps = flow?.Steps ?? new List<Step>();

        foreach (var step in steps.OrderBy(s => s.Number))
        {
            var isJumpTarget = topologyContext.JumpTargetStepNumbers.Contains(step.Number);
            var isMacro = string.Equals(step.Kind, "macro", StringComparison.OrdinalIgnoreCase);

            if (isJumpTarget)
            {
                legacySb.AppendLine($"lbl_step{step.Number}:");
            }

            if (isMacro)
            {
                var macroTarget = topologyContext.MacroCallMap.GetValueOrDefault(step.Number, "SubFlow");
                legacySb.AppendLine($"        ! STEP {step.Number}: {step.Label}");
                legacySb.AppendLine($"        {macroTarget};");

                // Render outgoing transition wait for macro step (same as normal step)
                var macroOutgoing = topologyContext.TransitionDetails
                    .Where(td => td.FromStepNumber == step.Number)
                    .FirstOrDefault();
                if (macroOutgoing != null && !string.IsNullOrWhiteSpace(macroOutgoing.Condition)
                    && !string.Equals(macroOutgoing.Condition, "always", StringComparison.OrdinalIgnoreCase))
                {
                    var waitInst = TopologyAnalyzer.FormatRapidWaitInstruction(macroOutgoing.Condition);
                    if (!string.IsNullOrWhiteSpace(waitInst))
                        legacySb.AppendLine($"        {waitInst}");
                }
            }
            else
            {
                legacySb.AppendLine($"        ! STEP {step.Number}: {step.Label}");
                if (doc.Steps != null && doc.Steps.TryGetValue(step.Number, out var snippet) && !string.IsNullOrWhiteSpace(snippet))
                {
                    legacySb.AppendLine(IndentText(snippet, 8));
                }
                else
                {
                    legacySb.AppendLine($"        ! Step {step.Number} snippet empty");
                }
            }

            legacySb.AppendLine();
        }

        return legacySb.ToString().TrimEnd();
    }

    private static void RenderExecutionBlock(StringBuilder sb, ExecutionBlock block, SnippetMapDocument doc, TopologyContext topologyContext, int indent)
    {
        var pad = new string(' ', indent);

        switch (block)
        {
            case StepExecutionBlock stepBlock:
                RenderSingleStepBlock(sb, stepBlock, doc, topologyContext, indent);
                break;

            case SelectionBranchExecutionBlock branchBlock:
                RenderSelectionBranchBlock(sb, branchBlock, doc, topologyContext, indent);
                break;

            case LoopBackExecutionBlock loopBlock:
                var waitInst = TopologyAnalyzer.FormatRapidWaitInstruction(loopBlock.Condition);
                if (!string.IsNullOrWhiteSpace(waitInst))
                {
                    sb.AppendLine($"{pad}{waitInst}");
                }
                sb.AppendLine($"{pad}GOTO lbl_step{loopBlock.ToStepNumber};");
                sb.AppendLine();
                break;
        }
    }

    private static void RenderSingleStepBlock(StringBuilder sb, StepExecutionBlock stepBlock, SnippetMapDocument doc, TopologyContext topologyContext, int indent)
    {
        var pad = new string(' ', indent);
        var step = stepBlock.Step;

        if (topologyContext.JumpTargetStepNumbers.Contains(step.Number))
        {
            sb.AppendLine($"lbl_step{step.Number}:");
        }

        var isMacro = string.Equals(step.Kind, "macro", StringComparison.OrdinalIgnoreCase);

        if (isMacro)
        {
            var macroTarget = topologyContext.MacroCallMap.GetValueOrDefault(step.Number, "SubFlow");
            sb.AppendLine($"{pad}! STEP {step.Number}: {step.Label}");
            sb.AppendLine($"{pad}{macroTarget};");

            // Render outgoing transition wait for macro step (same as normal step)
            if (!string.IsNullOrWhiteSpace(stepBlock.WaitCondition))
            {
                var waitInst = TopologyAnalyzer.FormatRapidWaitInstruction(stepBlock.WaitCondition);
                if (!string.IsNullOrWhiteSpace(waitInst))
                    sb.AppendLine($"{pad}{waitInst}");
            }
        }
        else
        {
            sb.AppendLine($"{pad}! STEP {step.Number}: {step.Label}");

            string? snippet = null;
            if (doc.Steps != null)
            {
                doc.Steps.TryGetValue(step.Number, out snippet);
            }

            if (!string.IsNullOrWhiteSpace(snippet))
            {
                sb.AppendLine(IndentText(snippet, indent));
            }
            else
            {
                sb.AppendLine($"{pad}! Step {step.Number} snippet empty");
            }

            if (!string.IsNullOrWhiteSpace(stepBlock.WaitCondition))
            {
                var waitInst = TopologyAnalyzer.FormatRapidWaitInstruction(stepBlock.WaitCondition);
                if (!string.IsNullOrWhiteSpace(waitInst))
                {
                    if (snippet == null || !snippet.Contains(waitInst, StringComparison.OrdinalIgnoreCase))
                    {
                        sb.AppendLine($"{pad}{waitInst}");
                    }
                }
            }
        }

        sb.AppendLine();
    }

    private static void RenderSelectionBranchBlock(StringBuilder sb, SelectionBranchExecutionBlock branchBlock, SnippetMapDocument doc, TopologyContext topologyContext, int indent)
    {
        var pad = new string(' ', indent);

        if (branchBlock.IsIntegerVariablePattern)
        {
            var firstCond = branchBlock.Arms.FirstOrDefault()?.Condition ?? "Mode = 1";
            var varName = firstCond.Split('=')[0].Trim();

            sb.AppendLine($"{pad}TEST {varName}");
            foreach (var arm in branchBlock.Arms)
            {
                var valMatch = System.Text.RegularExpressions.Regex.Match(arm.Condition, @"=\s*(\d+)");
                var valStr = valMatch.Success ? valMatch.Groups[1].Value : "1";

                sb.AppendLine($"{pad}  CASE {valStr}:");
                foreach (var armStepBlock in arm.ArmStepBlocks)
                {
                    RenderSingleStepBlock(sb, armStepBlock, doc, topologyContext, indent + 4);
                }
                if (arm.ArmStepBlocks.Count == 0)
                {
                    var innerPad = new string(' ', indent + 4);
                    sb.AppendLine($"{innerPad}GOTO lbl_step{arm.StartStepNumber};");
                }
            }
            sb.AppendLine($"{pad}ENDTEST;");
        }
        else
        {
            for (int i = 0; i < branchBlock.Arms.Count; i++)
            {
                var arm = branchBlock.Arms[i];
                var cond = arm.Condition;

                if (i == 0)
                {
                    sb.AppendLine($"{pad}IF {cond} THEN");
                }
                else if (i == branchBlock.Arms.Count - 1 && (cond.StartsWith("Not", StringComparison.OrdinalIgnoreCase) || cond.StartsWith("!") || string.IsNullOrWhiteSpace(cond)))
                {
                    sb.AppendLine($"{pad}ELSE");
                }
                else
                {
                    sb.AppendLine($"{pad}ELSIF {cond} THEN");
                }

                foreach (var armStepBlock in arm.ArmStepBlocks)
                {
                    RenderSingleStepBlock(sb, armStepBlock, doc, topologyContext, indent + 4);
                }
                if (arm.ArmStepBlocks.Count == 0)
                {
                    var innerPad = new string(' ', indent + 4);
                    sb.AppendLine($"{innerPad}GOTO lbl_step{arm.StartStepNumber};");
                }
            }
            sb.AppendLine($"{pad}END_IF;");
        }

        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(branchBlock.MergeCondition))
        {
            var mergeWait = TopologyAnalyzer.FormatRapidWaitInstruction(branchBlock.MergeCondition);
            if (!string.IsNullOrWhiteSpace(mergeWait))
            {
                sb.AppendLine($"{pad}! === FIRST MERGE POINT (Transition: {branchBlock.MergeCondition}) ===");
                sb.AppendLine($"{pad}{mergeWait}");
                sb.AppendLine();
            }
        }
    }

    private static string IndentText(string text, int spaces)
    {
        var pad = new string(' ', spaces);
        var lines = text.Split('\n');
        var sb = new StringBuilder();
        foreach (var l in lines)
        {
            var trimmed = l.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine(pad + trimmed);
            }
        }
        return sb.ToString().TrimEnd();
    }
}
