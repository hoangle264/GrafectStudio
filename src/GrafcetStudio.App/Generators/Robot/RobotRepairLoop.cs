using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using GrafcetStudio.App.Services.Ai;
using GrafcetStudio.App.Generators.Robot.RapidAst;

namespace GrafcetStudio.App.Generators.Robot;

public record RepairResult(
    bool Success,
    SnippetMapDocument? Document,
    List<string> RemainingErrors
);

public class RobotRepairLoop
{
    private readonly IAiCompletionService? _aiService;
    private readonly RobotSnippetParser _parser;

    public RobotRepairLoop(IAiCompletionService? aiService)
    {
        _aiService = aiService;
        _parser = new RobotSnippetParser();
    }

    public async Task<RepairResult> TryRepairAsync(
        SnippetMapDocument brokenDoc,
        IReadOnlyList<LintError> lintErrors,
        string originalUserPrompt,
        int maxRetries = 2)
    {
        if (_aiService == null || lintErrors == null || lintErrors.Count == 0)
        {
            return new RepairResult(false, brokenDoc, lintErrors?.Select(e => e.ErrorMessage).ToList() ?? new List<string>());
        }

        var currentDoc = brokenDoc;
        var currentErrors = lintErrors.Select(e => $"Step {e.StepNumber}: {e.ErrorMessage} in snippet '{e.CodeSnippet}'").ToList();

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            var repairSystemPrompt = @"You are a Code Repair Engine for ABB RAPID Robot Snippet Map.
Your previous response contained syntax/lint errors.
Fix ONLY the errors described in the error report.
Return the complete corrected JSON conforming to 'grafectstudio/robot-snippet-map/v1'.
Return ONLY raw valid JSON.";

            var repairUserPrompt = $@"=== ORIGINAL PROMPT ===
{originalUserPrompt}

=== CURRENT BROKEN JSON ===
{JsonSerializer.Serialize(currentDoc)}

=== LINT ERROR REPORT ===
{string.Join("\n", currentErrors)}

Please fix all syntax and lint errors listed above and return the corrected complete JSON object.";

            var jsonPayload = JsonSerializer.Serialize(new { systemPrompt = repairSystemPrompt, userPrompt = repairUserPrompt });
            var sanitized = new SanitizedAiRequest($"repair-{attempt}", "robot-codegen-repair", repairUserPrompt, jsonPayload);
            var req = new AiCompletionRequest(sanitized);

            try
            {
                var completionResult = await _aiService.CompleteAsync(req);
                if (!completionResult.Ok || string.IsNullOrWhiteSpace(completionResult.RawText))
                {
                    continue;
                }

                var parseResult = _parser.Parse(completionResult.RawText);
                if (parseResult.IsOk && parseResult.Document != null)
                {
                    return new RepairResult(true, parseResult.Document, new List<string>());
                }
            }
            catch
            {
                // Continue retry loop
            }
        }

        return new RepairResult(false, currentDoc, currentErrors);
    }
}
