using System;
using System.Text;

namespace GrafcetStudio.App.Generators.Robot;

public class TeachListGenerator
{
    public string GenerateTeachList(SnippetMapDocument doc, string flowName = "main")
    {
        var sb = new StringBuilder();

        sb.AppendLine("=================================================");
        sb.AppendLine($"  ROBOT POSITION TEACH CHECKLIST ({doc.Platform})");
        sb.AppendLine($"  Module: {doc.Module} (Flow: {flowName})");
        sb.AppendLine($"  Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine("=================================================");
        sb.AppendLine();
        sb.AppendLine("⚠ IMPORTANT SAFETY NOTICE:");
        sb.AppendLine("  1. Verify all mechanical physical clearance before moving.");
        sb.AppendLine("  2. Teach positions in T1 (Manual Low Speed) mode on FlexPendant.");
        sb.AppendLine("  3. Test full cycle step-by-step before automatic execution.");
        sb.AppendLine();

        sb.AppendLine("--- POSITIONS TO TEACH (robtarget) ---");
        if (doc.Positions != null && doc.Positions.Count > 0)
        {
            int idx = 1;
            foreach (var pos in doc.Positions)
            {
                var desc = string.IsNullOrWhiteSpace(pos.Description) ? "No description" : pos.Description;
                sb.AppendLine($"  [{idx}] Position Name : {pos.Name}");
                sb.AppendLine($"      Type          : {pos.MotionType}");
                sb.AppendLine($"      Speed         : {pos.Speed}");
                sb.AppendLine($"      Description   : {desc}");
                sb.AppendLine($"      Status        : [ ] TAUGHT ON FLEXPENDANT");
                sb.AppendLine();
                idx++;
            }
        }
        else
        {
            sb.AppendLine("  No position targets declared.");
            sb.AppendLine();
        }

        sb.AppendLine("--- TOOLS TO CALIBRATE (tooldata) ---");
        if (doc.Tools != null && doc.Tools.Count > 0)
        {
            int idx = 1;
            foreach (var tool in doc.Tools)
            {
                var desc = string.IsNullOrWhiteSpace(tool.Description) ? "TCP Calibration required" : tool.Description;
                sb.AppendLine($"  [{idx}] Tool Name     : {tool.Name}");
                sb.AppendLine($"      Description   : {desc}");
                sb.AppendLine($"      Status        : [ ] CALIBRATED");
                sb.AppendLine();
                idx++;
            }
        }
        else
        {
            sb.AppendLine("  No tools declared.");
            sb.AppendLine();
        }

        sb.AppendLine("--- DIGITAL SIGNALS TO VERIFY (I/O) ---");
        if (doc.Signals != null && doc.Signals.Count > 0)
        {
            foreach (var sig in doc.Signals)
            {
                sb.AppendLine($"  - Signal: {sig.Name} ({sig.Type}) -> Hardware Alias: {sig.Alias}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("=================================================");
        sb.AppendLine("  Checklist Complete Sign-off: _________________");
        sb.AppendLine("=================================================");

        return sb.ToString();
    }
}
