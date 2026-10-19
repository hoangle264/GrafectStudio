using System;
using System.IO;
using System.Text;

namespace GrafcetStudio.App.Generators.Robot;

public class RobotRagLoader
{
    private readonly string _basePath;

    public RobotRagLoader(string? basePath = null)
    {
        _basePath = basePath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "rag");
    }

    public string LoadRagContent(string platform = "abb")
    {
        var sb = new StringBuilder();
        var platformDir = Path.Combine(_basePath, platform.ToLowerInvariant());

        if (!Directory.Exists(platformDir))
        {
            var fallbackDir = Path.Combine(Directory.GetCurrentDirectory(), "assets", "rag", platform.ToLowerInvariant());
            if (Directory.Exists(fallbackDir))
            {
                platformDir = fallbackDir;
            }
        }

        if (Directory.Exists(platformDir))
        {
            var rulesFile = Path.Combine(platformDir, "rules.md");
            if (File.Exists(rulesFile))
            {
                sb.AppendLine("## Supplemental Rules");
                sb.AppendLine(File.ReadAllText(rulesFile));
                sb.AppendLine();
            }

            var irPatternsFile = Path.Combine(platformDir, "ir_patterns.md");
            if (File.Exists(irPatternsFile))
            {
                sb.AppendLine("## RAG IR Patterns");
                sb.AppendLine(File.ReadAllText(irPatternsFile));
                sb.AppendLine();
            }

            var patternsSubDir = Path.Combine(platformDir, "patterns");
            if (Directory.Exists(patternsSubDir))
            {
                var mdFiles = Directory.GetFiles(patternsSubDir, "*.md");
                foreach (var mdFile in mdFiles)
                {
                    sb.AppendLine($"## Pattern ({Path.GetFileNameWithoutExtension(mdFile)})");
                    sb.AppendLine(File.ReadAllText(mdFile));
                    sb.AppendLine();
                }
            }
        }

        return sb.ToString();
    }
}
