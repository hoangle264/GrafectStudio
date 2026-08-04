using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using GrafcetStudio.Domain.Models;

namespace GrafcetStudio.App.Generators.Robot;

public class RobotPromptBuilder
{
    private readonly RobotRagLoader _ragLoader;

    public RobotPromptBuilder(RobotRagLoader? ragLoader = null)
    {
        _ragLoader = ragLoader ?? new RobotRagLoader();
    }

    public (string SystemPrompt, string UserPrompt) BuildPrompts(CodegenPayload payload, string targetFlowName = "main")
    {
        var ragContent = _ragLoader.LoadRagContent(payload.Platform ?? "abb");

        var sbSys = new StringBuilder();
        sbSys.AppendLine("You are an expert industrial robot motion planner integrated into GrafectStudio.");
        sbSys.AppendLine("Your ONLY task: convert the provided Grafect sequence flow and variables into a JSON IR object following this schema exactly.");
        sbSys.AppendLine("Do NOT generate RAPID syntax text directly. Do NOT add explanation. Output ONLY valid JSON matching schema 'grafectstudio/robot-ir/v1'.");
        sbSys.AppendLine();
        sbSys.AppendLine("## Whitelist Instruction Types");
        sbSys.AppendLine("- MoveJ (target, tool, optional offset)");
        sbSys.AppendLine("- MoveL (target, tool, optional offset)");
        sbSys.AppendLine("- MoveAbsJ (target, tool)");
        sbSys.AppendLine("- SetDO (signal, value: 0 or 1)");
        sbSys.AppendLine("- WaitDI (signal, value: 0 or 1)");
        sbSys.AppendLine("- WaitTime (seconds)");
        sbSys.AppendLine("- SetAO (signal, value)");
        sbSys.AppendLine("- WaitAI (signal, value)");
        sbSys.AppendLine();
        sbSys.AppendLine("## Strict Rules");
        sbSys.AppendLine("1. Only reference positions declared in user variables (or reasonable home/wait targets).");
        sbSys.AppendLine("2. Only reference signals declared in user variables.");
        sbSys.AppendLine("3. After SetDO, add a short WaitTime (e.g. 0.3s) for mechanical stability.");
        sbSys.AppendLine("4. Init block must reset all DO signals to 0, then move to home position using MoveAbsJ.");
        sbSys.AppendLine("5. Every instruction target position must be declared in the 'positions' array.");
        sbSys.AppendLine("6. Every instruction signal must be declared in the 'signals' array.");
        sbSys.AppendLine();
        if (!string.IsNullOrWhiteSpace(ragContent))
        {
            sbSys.AppendLine(ragContent);
        }

        var sbUser = new StringBuilder();
        sbUser.AppendLine($"Target Flow: {targetFlowName}");
        sbUser.AppendLine("Platform: " + (payload.Platform ?? "ABB_RAPID"));
        sbUser.AppendLine();

        sbUser.AppendLine("### Declared Variables:");
        if (payload.Variables != null && payload.Variables.Count > 0)
        {
            foreach (var v in payload.Variables)
            {
                sbUser.AppendLine($"- Name: {v.Label}, Type/Format: {v.Format}, Address: {v.Address}");
            }
        }
        else
        {
            sbUser.AppendLine("No explicit variables provided.");
        }
        sbUser.AppendLine();

        sbUser.AppendLine("### Grafect Sequence Flows:");
        if (payload.Flows != null && payload.Flows.Count > 0)
        {
            foreach (var flow in payload.Flows)
            {
                var flowName = flow.Name ?? flow.Diagram?.Name ?? "main";
                sbUser.AppendLine($"Flow [{flowName}]:");
                sbUser.AppendLine("  Steps:");
                foreach (var step in flow.Steps)
                {
                    sbUser.AppendLine($"    - Step #{step.Number} (ID: {step.Id}, Label: {step.Label}):");
                    if (step.Actions != null && step.Actions.Count > 0)
                    {
                        foreach (var act in step.Actions)
                        {
                            sbUser.AppendLine($"        Action: Qualifier={act.Qualifier}, Variable={act.Variable}, Address={act.Address}");
                        }
                    }
                }
                sbUser.AppendLine("  Transitions:");
                foreach (var tr in flow.Transitions)
                {
                    sbUser.AppendLine($"    - Transition ID: {tr.Id}, Condition: {tr.Condition}");
                }
            }
        }

        return (sbSys.ToString(), sbUser.ToString());
    }
}
