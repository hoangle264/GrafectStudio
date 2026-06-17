namespace GrafcetStudio.App.Services.Ai;

public static class AiPromptBuilder
{
    public static string BuildSystemPrompt(string intent)
    {
        var intentRequirements = intent switch
        {
            "clone-variable" => "Return data.source and data.variable. The new variable must be safe, deterministic, and must not overwrite existing variables.",
            "map-io" => "Return data.entries only. Each entry must contain physicalIOId, appVariable, status, and optional matchScore.",
            "create-flow" => "Return data.flow only. Keep the flow minimal, with valid ids, steps, transitions, and connections.",
            _ => "Return data.bucket and data.variable only. Use bucket \"user\" unless sanitized context clearly requires another safe bucket."
        };

        return string.Join("\n", new[]
        {
            "You are Grafcet Studio's proposal generator.",
            "Return exactly one JSON object and no markdown, comments, or explanatory text.",
            $"The JSON object must use schemaVersion \"{AiContractGuard.SchemaVersion}\" and intent \"{intent}\".",
            "The JSON object must contain: schemaVersion, id, intent, status, requestId, summary, warnings, data.",
            "status must be \"draft\". The app will validate and preview before any apply step.",
            "Do not include secrets, local file paths, machine names, API keys, host config, or unsanitized project data.",
            "Do not ask the app to mutate state directly. Produce proposals only.",
            intentRequirements
        });
    }

    public static string BuildUserPrompt(SanitizedAiRequest request)
        => string.Join("\n", new[]
        {
            "User request and sanitized context follow.",
            "Only use this sanitized AiRequest JSON; do not infer hidden project state.",
            request.Json
        });
}
