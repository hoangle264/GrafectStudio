using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GrafcetStudio.App.Generators.Robot.RapidAst;
using GrafcetStudio.App.Services.Ai;
using GrafcetStudio.Domain.Models;

namespace GrafcetStudio.App.Generators.Robot;

public class AbbRapidGenerator : ICodeGenerator
{
    public string Platform => "abb-rapid";

    private readonly TopologyAnalyzer _topologyAnalyzer;
    private readonly RobotSnippetPromptBuilder _promptBuilder;
    private readonly RobotSnippetParser _snippetParser;
    private readonly RapidAstLinter _astLinter;
    private readonly RobotSkeletonEngine _skeletonEngine;
    private readonly TeachListGenerator _teachListGenerator;
    private readonly RobotRepairLoop _repairLoop;
    private readonly IAiCompletionService? _aiCompletionService;

    public AbbRapidGenerator(IAiCompletionService? aiCompletionService = null)
    {
        _topologyAnalyzer = new TopologyAnalyzer();
        _promptBuilder = new RobotSnippetPromptBuilder();
        _snippetParser = new RobotSnippetParser();
        _astLinter = new RapidAstLinter();
        _skeletonEngine = new RobotSkeletonEngine();
        _teachListGenerator = new TeachListGenerator();
        _repairLoop = new RobotRepairLoop(aiCompletionService);
        _aiCompletionService = aiCompletionService;
    }

    public IEnumerable<CodegenFile> GenerateFiles(CodegenPayload payload)
    {
        var flows = payload?.Flows != null && payload.Flows.Count > 0
            ? payload.Flows
            : new List<FlowInfo> { new FlowInfo { Name = "main" } };

        var generatedFiles = new List<CodegenFile>();

        // ── Check AI service configuration ──────────────────────────────────
        if (_aiCompletionService == null || _aiCompletionService is MockAiCompletionService)
        {
            var targetFlow = flows.FirstOrDefault()?.Name ?? "main";
            var flowNameClean = SanitizeFilename(targetFlow);

            return FailureFiles(flowNameClean,
                reason: "AI_SERVICE_NOT_CONFIGURED",
                details: new[]
                {
                    "No real AI completion service is registered.",
                    "Set environment variables before launching the app:",
                    "  GRAFCETSTUDIO_AI_MODE=gemini",
                    "  GEMINI_API_KEY=<your-key>",
                    "Robot code generation requires a live AI connection and CANNOT use a fallback.",
                    "A fallback would produce incorrect motion sequences that may damage equipment."
                });
        }

        // Build flowById lookup so TopologyAnalyzer can resolve macro flow names
        var flowById = flows
            .Where(f => !string.IsNullOrWhiteSpace(f.Id))
            .ToDictionary(f => f.Id!, f => f, StringComparer.OrdinalIgnoreCase);

        foreach (var flow in flows)
        {
            var rawFlowName = !string.IsNullOrWhiteSpace(flow.Name) ? flow.Name : "main";
            var flowNameClean = SanitizeFilename(rawFlowName);

            // 1. Topology Analysis
            var topologyContext = _topologyAnalyzer.Analyze(flow, flowById);

            // 2. Build Prompts
            var (systemPrompt, userPrompt) = _promptBuilder.BuildPrompts(flow, payload.Variables ?? new List<DeviceVariable>(), topologyContext, flowNameClean);

            // Log Prompt Sent
            var debugTransitions = string.Join("\n", (flow.Transitions ?? new List<Transition>())
                .Select(t => $"  [{t.Id}] FROM=[{string.Join(",", t.FromStepIds ?? new List<string>())}] TO=[{string.Join(",", t.ToStepIds ?? new List<string>())}] COND=\"{t.Condition}\""));
            var promptLogContent = FormatPromptLog(flowNameClean, systemPrompt, userPrompt, debugTransitions);

            SavePromptLogToDisk(flowNameClean, promptLogContent);

            // 3. Call AI Service
            AiCompletionResult result;
            try
            {
                var jsonPayload = System.Text.Json.JsonSerializer.Serialize(new { systemPrompt, userPrompt });
                var sanitized = new SanitizedAiRequest($"robot-gen-{flowNameClean}", "robot-codegen", userPrompt, jsonPayload);
                var req = new AiCompletionRequest(sanitized);
                result = Task.Run(() => _aiCompletionService.CompleteAsync(req)).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                generatedFiles.AddRange(FailureFiles(flowNameClean,
                    reason: "AI_REQUEST_EXCEPTION",
                    details: new[]
                    {
                        $"An exception occurred while calling the AI service for flow {flowNameClean}: {ex.GetType().Name}",
                        $"Message: {ex.Message}",
                        "Check your network connection and API key validity."
                    }));
                continue;
            }

            if (!result.Ok || string.IsNullOrWhiteSpace(result.RawText))
            {
                var aiErrors = result.Errors != null && result.Errors.Count > 0
                ? result.Errors.ToArray()
                : new[] { "AI service returned an empty or unsuccessful response." };

                generatedFiles.AddRange(FailureFiles(flowNameClean,
                    reason: "AI_RESPONSE_FAILED",
                    details: aiErrors.Prepend("AI call did not succeed:").ToArray()));
                continue;
            }

            // 4. Parse Snippet Map JSON
            var parseResult = _snippetParser.Parse(result.RawText);
            SnippetMapDocument? doc = parseResult.Document;

            if (!parseResult.IsOk || doc == null)
            {
                // Try repair loop
                var repairTask = Task.Run(() => _repairLoop.TryRepairAsync(
                    doc ?? new SnippetMapDocument { Module = flowNameClean },
                    parseResult.Errors.Select(e => new LintError(0, "", e)).ToList(),
                    userPrompt));
                var repairRes = repairTask.GetAwaiter().GetResult();

                if (repairRes.Success && repairRes.Document != null)
                {
                    doc = repairRes.Document;
                }
                else
                {
                    generatedFiles.AddRange(FailureFiles(flowNameClean,
                        reason: "AI_RESPONSE_INVALID_SCHEMA",
                        details: parseResult.Errors
                            .Prepend($"AI returned JSON that does not conform to schema 'grafectstudio/robot-snippet-map/v1'.")
                            .ToArray()));
                    continue;
                }
            }

            // 5. AST Linter
            var lintErrors = _astLinter.Lint(doc, payload.Variables ?? new List<DeviceVariable>(), topologyContext);
            if (lintErrors.Count > 0)
            {
                // Try repair loop
                var repairTask = Task.Run(() => _repairLoop.TryRepairAsync(doc, lintErrors, userPrompt));
                var repairRes = repairTask.GetAwaiter().GetResult();

                if (repairRes.Success && repairRes.Document != null)
                {
                    doc = repairRes.Document;
                }
                else
                {
                    var lintErrMsgs = lintErrors.Select(e => $"Step {e.StepNumber}: {e.ErrorMessage}").ToArray();
                    generatedFiles.AddRange(FailureFiles(flowNameClean,
                        reason: "AST_LINTER_VALIDATION_FAILED",
                        details: lintErrMsgs.Prepend("RAPID AST Linter validation failed:").ToArray()));
                    continue;
                }
            }

            // 6. Skeleton Engine Rendering
            var rapidCode = _skeletonEngine.Render(doc, topologyContext, flow, flowNameClean);
            var teachList = _teachListGenerator.GenerateTeachList(doc, flowNameClean);

            generatedFiles.Add(new CodegenFile { Path = $"{flowNameClean}.mod", Content = rapidCode });
            generatedFiles.Add(new CodegenFile { Path = $"{flowNameClean}_teach.txt", Content = teachList });
            generatedFiles.Add(new CodegenFile { Path = $"{flowNameClean}_prompt.log", Content = promptLogContent });
        }

        return generatedFiles;
    }

    private static string FormatPromptLog(string flowName, string systemPrompt, string userPrompt, string? debugTransitions = null)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("================================================================================");
        sb.AppendLine("ROBOT CODEGEN AI PROMPT LOG");
        sb.AppendLine($"Flow Name : {flowName}");
        sb.AppendLine("Platform  : ABB_RAPID");
        sb.AppendLine($"Timestamp : {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
        sb.AppendLine("================================================================================");
        sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(debugTransitions))
        {
            sb.AppendLine("--- [DEBUG: RAW TRANSITION DATA] ---");
            sb.AppendLine(debugTransitions);
            sb.AppendLine();
        }
        sb.AppendLine("--- [SYSTEM PROMPT] ---");
        sb.AppendLine(systemPrompt);
        sb.AppendLine();
        sb.AppendLine("--- [USER PROMPT] ---");
        sb.AppendLine(userPrompt);
        sb.AppendLine();
        sb.AppendLine("================================================================================");
        sb.AppendLine("END OF PROMPT LOG");
        sb.AppendLine("================================================================================");
        return sb.ToString();
    }


    private static void SavePromptLogToDisk(string flowName, string content)
    {
        try
        {
            var logsDir = Path.Combine(Directory.GetCurrentDirectory(), "logs");
            Directory.CreateDirectory(logsDir);
            File.WriteAllText(Path.Combine(logsDir, $"robot_prompt_{flowName}.log"), content, System.Text.Encoding.UTF8);
            File.WriteAllText(Path.Combine(logsDir, "prompt_sent.log"), content, System.Text.Encoding.UTF8);

            var baseLogsDir = Path.Combine(AppContext.BaseDirectory, "logs");
            if (!string.Equals(Path.GetFullPath(logsDir), Path.GetFullPath(baseLogsDir), StringComparison.OrdinalIgnoreCase))
            {
                Directory.CreateDirectory(baseLogsDir);
                File.WriteAllText(Path.Combine(baseLogsDir, $"robot_prompt_{flowName}.log"), content, System.Text.Encoding.UTF8);
                File.WriteAllText(Path.Combine(baseLogsDir, "prompt_sent.log"), content, System.Text.Encoding.UTF8);
            }
        }
        catch
        {
        }
    }

    private static IEnumerable<CodegenFile> FailureFiles(string flowName, string reason, string[] details)
    {
        var lines = new System.Text.StringBuilder();
        lines.AppendLine("! ===========================================================");
        lines.AppendLine("! ROBOT CODEGEN FAILED — DO NOT LOAD THIS FILE TO CONTROLLER");
        lines.AppendLine("! ===========================================================");
        lines.AppendLine($"! Reason : {reason}");
        lines.AppendLine($"! Flow   : {flowName}");
        lines.AppendLine($"! Time   : {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
        lines.AppendLine("!");
        foreach (var d in details)
            lines.AppendLine($"! {d}");
        lines.AppendLine("!");
        lines.AppendLine("! To fix: ensure AI service is configured and the Grafcet flow");
        lines.AppendLine("! has correctly declared variables (POS, DO, DI formats).");
        lines.AppendLine("! ===========================================================");

        return new List<CodegenFile>
        {
            new CodegenFile
            {
                Path    = $"{flowName}.mod",
                Content = lines.ToString()
            }
        };
    }

    private static string SanitizeFilename(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "main";
        var invalidChars = Path.GetInvalidFileNameChars();
        var clean = new string(name.Where(c => !invalidChars.Contains(c)).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(clean) ? "main" : clean;
    }
}
