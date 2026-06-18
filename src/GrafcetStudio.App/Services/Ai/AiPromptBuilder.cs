namespace GrafcetStudio.App.Services.Ai;

public static class AiPromptBuilder
{
    public static string BuildSystemPrompt(string intent)
    {
        var intentRequirements = intent switch
        {
            "clone-variable" => "Return data.source (object with label or id of the template to clone) and data.variables (array of 2 or more variable objects). Each variable needs label, format (same as source), kind, comment, source=\"manual\". Do NOT return data.variable (singular). Minimum 2 variables per proposal to be useful. Address fields are optional for struct-type variables.",
            "map-io" => "Return data.entries only. Each entry must contain physicalIOId, appVariable, status, and optional matchScore.",
            "create-flow" => "Return data.flow only. Keep the flow minimal, with valid ids, steps, transitions, and connections.",
            "create-structure" => "Return data.name (string) and data.signals (array of objects with name, dataType, varType, comment). dataType must be one of: Bool, Int, Real, Word, DWord, Time. varType must be one of: Input, Output, Var. Do not include id, categoryId, address, deviceId, or internal metadata. Do not propose a structure name already listed in existingStructures.",
            _ => "Return data.bucket and data.variable only. Use bucket \"user\" unless sanitized context clearly requires another safe bucket."
        };

        return string.Join("\n", new[]
        {
            "You are Grafcet Studio's proposal generator.",
            "Return exactly one JSON object and no markdown, comments, or explanatory text.",
            $"The JSON object must use schemaVersion \"{AiContractGuard.SchemaVersion}\" and intent \"{intent}\".",
            "The JSON object must contain: schemaVersion, id, intent, status, requestId, summary, warnings, data.",
            "For create-variable, data.variable must be a single object. For clone-variable, data.variables must be an array and data.variable must not be used.",
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
