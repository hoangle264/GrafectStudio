using System.Text.Json;

namespace GrafcetStudio.App.Services.Ai;

public static class AiIntegrationSecurityValidation
{
    public static AiIntegrationSecurityValidationResult Run()
    {
        var errors = new List<string>();
        var requestJson = JsonSerializer.Serialize(new
        {
            schemaVersion = AiContractGuard.SchemaVersion,
            id = "ai-req-p10-host-validation",
            intent = "create-variable",
            message = "Create a safe variable proposal from sanitized host context.",
            context = new
            {
                variables = new[]
                {
                    new
                    {
                        id = "var-host-1",
                        label = "Host_StartCommand",
                        format = "BOOL",
                        address = "MR930",
                        apiKey = "AIzaFakeSecret",
                        filePath = "C:\\Users\\Nitro\\secret.csv",
                        comment = "secret=sk-test-secret"
                    }
                },
                selection = new
                {
                    diagramId = "diagram-host-1",
                    machineName = "BUILD-SERVER-SECRET",
                    templatePath = "templates\\host.tpl"
                },
                localConfig = new { token = "sk-test-secret" },
                project = new { machineName = "BUILD-SERVER-SECRET" }
            }
        });

        var sanitized = AiContractGuard.SanitizeRequestJson(requestJson);
        Assert(sanitized.Ok && sanitized.Request is not null, "host sanitizer should accept valid AI request JSON.", errors);
        Assert(sanitized.Request is not null && !ContainsForbiddenText(sanitized.Request.Json), "host sanitizer must remove path-like, machine, template, and secret-like fields.", errors);

        var malformed = AiContractGuard.SanitizeRequestJson("{ bad json");
        Assert(!malformed.Ok && malformed.Errors.Count > 0, "host sanitizer should reject malformed JSON.", errors);

        var missingFields = AiContractGuard.SanitizeRequestJson(JsonSerializer.Serialize(new { schemaVersion = AiContractGuard.SchemaVersion, intent = "create-variable", context = new { } }));
        Assert(!missingFields.Ok && missingFields.Errors.Count > 0, "host sanitizer should reject missing required fields.", errors);

        var unsupportedSchema = AiContractGuard.SanitizeRequestJson(JsonSerializer.Serialize(new { schemaVersion = "999.0.0", id = "ai-req-bad-schema", intent = "create-variable", message = "Create variable", context = new { } }));
        Assert(!unsupportedSchema.Ok, "host sanitizer should reject unsupported schema versions.", errors);

        return new AiIntegrationSecurityValidationResult(errors.Count == 0, errors, sanitized.Request?.Json ?? string.Empty);
    }

    private static void Assert(bool condition, string message, List<string> errors)
    {
        if (!condition) errors.Add(message);
    }

    private static bool ContainsForbiddenText(string value)
        => value.Contains("C:\\Users", StringComparison.OrdinalIgnoreCase)
            || value.Contains("BUILD-SERVER", StringComparison.OrdinalIgnoreCase)
            || value.Contains("templates\\", StringComparison.OrdinalIgnoreCase)
            || value.Contains("apiKey", StringComparison.OrdinalIgnoreCase)
            || value.Contains("secret", StringComparison.OrdinalIgnoreCase)
            || value.Contains("password", StringComparison.OrdinalIgnoreCase)
            || value.Contains("token", StringComparison.OrdinalIgnoreCase)
            || value.Contains("machineName", StringComparison.OrdinalIgnoreCase)
            || value.Contains("templatePath", StringComparison.OrdinalIgnoreCase)
            || value.Contains("filePath", StringComparison.OrdinalIgnoreCase)
            || value.Contains("AIza", StringComparison.OrdinalIgnoreCase)
            || value.Contains("sk-test-secret", StringComparison.OrdinalIgnoreCase);
}

public sealed record AiIntegrationSecurityValidationResult(bool Ok, IReadOnlyList<string> Errors, string SanitizedJson);
