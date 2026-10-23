using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GrafcetStudio.App.Services.Ai;
using GrafcetStudio.Domain.Models;

namespace GrafcetStudio.App.Generators.Robot;

public class AbbRapidGenerator : ICodeGenerator
{
    public string Platform => "abb-rapid";

    private const int MaxRepairAttempts = 2;

    private readonly TopologyAnalyzer _topologyAnalyzer;
    private readonly RobotRapidPromptBuilder _promptBuilder;
    private readonly IAiCompletionService? _aiCompletionService;

    public AbbRapidGenerator(IAiCompletionService? aiCompletionService = null)
    {
        _topologyAnalyzer = new TopologyAnalyzer();
        _promptBuilder = new RobotRapidPromptBuilder();
        _aiCompletionService = aiCompletionService;
    }

    public IEnumerable<CodegenFile> GenerateFiles(CodegenPayload payload)
    {
        var flows = payload?.Flows != null && payload.Flows.Count > 0
            ? payload.Flows
            : new List<FlowInfo> { new FlowInfo { Name = "main" } };

        var generatedFiles = new List<CodegenFile>();

        // ── Check AI service configuration ──────────────────────────────────
        if (_aiCompletionService == null || _aiCompletionService is MockAiCompletionService || _aiCompletionService is DynamicAiCompletionService { IsLive: false })
        {
            var targetFlow = flows.FirstOrDefault()?.Name ?? "main";
            var flowNameClean = SanitizeFilename(targetFlow);

            return FailureFiles(flowNameClean,
                reason: "AI_SERVICE_NOT_CONFIGURED",
                details: new[]
                {
                    "No real AI completion service is registered.",
                    "Open the AI panel settings (gear icon) and save a Gemini API key,",
                    "or set GEMINI_API_KEY before launching the app.",
                    "Robot code generation requires a live AI connection and CANNOT use a fallback.",
                    "A fallback would produce incorrect motion sequences that may damage equipment."
                });
        }

        // Build flowById lookup so TopologyAnalyzer can resolve macro flow names
        var flowById = flows
            .Where(f => !string.IsNullOrWhiteSpace(f.Id))
            .ToDictionary(f => f.Id!, f => f, StringComparer.OrdinalIgnoreCase);
        var variables = payload!.Variables ?? new List<DeviceVariable>();

        foreach (var flow in flows)
        {
            var rawFlowName = !string.IsNullOrWhiteSpace(flow.Name) ? flow.Name : "main";
            var flowNameClean = SanitizeFilename(rawFlowName);

            // 1. Topology analysis decides the control structure; the AI writes the module around it.
            RobotRapidPrompt prompt;
            try
            {
                var topologyContext = _topologyAnalyzer.Analyze(flow, flowById);
                prompt = _promptBuilder.BuildPrompts(flow, variables, topologyContext, flowNameClean);
            }
            catch (InvalidOperationException ex)
            {
                generatedFiles.AddRange(FailureFiles(flowNameClean, "TOPOLOGY_INVALID", new[] { ex.Message }));
                continue;
            }

            var promptLogContent = FormatPromptLog(flowNameClean, prompt.SystemPrompt, prompt.UserPrompt);
            SavePromptLogToDisk(flowNameClean, promptLogContent);

            // 2. Ask the AI for the module, then validate; feed errors back for a bounded number of repairs.
            var userPrompt = prompt.UserPrompt;
            string code = string.Empty;
            IReadOnlyList<string> errors = Array.Empty<string>();
            string? failureReason = null;

            for (var attempt = 0; attempt <= MaxRepairAttempts; attempt++)
            {
                AiCompletionResult result;
                try
                {
                    var jsonPayload = System.Text.Json.JsonSerializer.Serialize(new { systemPrompt = prompt.SystemPrompt, userPrompt });
                    var request = new SanitizedAiRequest($"robot-gen-{flowNameClean}-{attempt}", "robot-codegen", userPrompt, jsonPayload);
                    result = Task.Run(() => _aiCompletionService.CompleteAsync(new AiCompletionRequest(request))).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    failureReason = "AI_REQUEST_EXCEPTION";
                    errors = new[] { $"{ex.GetType().Name}: {ex.Message}", "Check your network connection and API key validity." };
                    break;
                }

                if (!result.Ok || string.IsNullOrWhiteSpace(result.RawText))
                {
                    failureReason = "AI_RESPONSE_FAILED";
                    errors = result.Errors is { Count: > 0 } ? result.Errors : new[] { "AI service returned an empty or unsuccessful response." };
                    break;
                }

                code = RapidModuleValidator.ExtractModule(result.RawText);
                errors = RapidModuleValidator.Validate(code, flowNameClean, prompt.AllowedIdentifiers, prompt.RequiredFragments);
                if (errors.Count == 0)
                {
                    failureReason = null;
                    break;
                }

                failureReason = "RAPID_VALIDATION_FAILED";
                userPrompt = BuildRepairPrompt(prompt.UserPrompt, code, errors);
            }

            if (failureReason != null)
            {
                generatedFiles.AddRange(FailureFiles(flowNameClean, failureReason, errors.Prepend("Generated code was rejected:").ToArray()));
                generatedFiles.Add(new CodegenFile { Path = $"{flowNameClean}_prompt.log", Content = promptLogContent });
                continue;
            }

            generatedFiles.Add(new CodegenFile { Path = $"{flowNameClean}.mod", Content = code + Environment.NewLine });
            generatedFiles.Add(new CodegenFile { Path = $"{flowNameClean}_prompt.log", Content = promptLogContent });
        }

        return generatedFiles;
    }

    private static string BuildRepairPrompt(string originalUserPrompt, string code, IReadOnlyList<string> errors)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(originalUserPrompt);
        sb.AppendLine();
        sb.AppendLine("=== YOUR PREVIOUS MODULE WAS REJECTED ===");
        sb.AppendLine(code);
        sb.AppendLine();
        sb.AppendLine("=== VALIDATION ERRORS ===");
        foreach (var error in errors) sb.AppendLine("- " + error);
        sb.AppendLine();
        sb.AppendLine("Fix every error above and output the complete corrected module. Follow all rules; output only the module.");
        return sb.ToString();
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
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Trace.WriteLine($"[Robot Codegen] Could not write prompt log for flow '{flowName}': {ex.Message}");
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
