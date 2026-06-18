using System.Text.Json;
using System.Text.Json.Nodes;

namespace GrafcetStudio.App.Services.Ai;

public static class AiContractGuard
{
    public const string SchemaVersion = "1.0.0";

    private static readonly HashSet<string> Intents = new(StringComparer.Ordinal)
    {
        "create-variable",
        "clone-variable",
        "map-io",
        "create-flow",
        "create-structure"
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private static readonly HashSet<string> SensitiveContextKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "apiKey",
        "apikey",
        "authorization",
        "connectionString",
        "deviceLibraryPath",
        "filePath",
        "hostName",
        "localConfig",
        "machineName",
        "outputPath",
        "password",
        "path",
        "secret",
        "secretToken",
        "sourcePath",
        "templatePath",
        "templateRootPath",
        "token"
    };

    public static AiRequestSanitizationResult SanitizeRequestJson(string requestJson)
    {
        if (string.IsNullOrWhiteSpace(requestJson))
        {
            return AiRequestSanitizationResult.Fail("AI request payload is empty.");
        }

        try
        {
            using var doc = JsonDocument.Parse(requestJson);
            var root = doc.RootElement;
            var errors = new List<string>();

            var schemaVersion = GetString(root, "schemaVersion");
            if (!string.Equals(schemaVersion, SchemaVersion, StringComparison.Ordinal))
            {
                errors.Add($"request.schemaVersion must be \"{SchemaVersion}\".");
            }

            var id = TrimToLimit(GetString(root, "id"), 120);
            if (string.IsNullOrWhiteSpace(id)) errors.Add("request.id is required.");

            var intent = GetString(root, "intent");
            if (!Intents.Contains(intent)) errors.Add("request.intent is unsupported.");

            var message = TrimToLimit(GetString(root, "message"), 2000);
            if (string.IsNullOrWhiteSpace(message)) errors.Add("request.message is required.");

            if (!root.TryGetProperty("context", out var context) || context.ValueKind != JsonValueKind.Object)
            {
                errors.Add("request.context must be an object.");
            }

            if (errors.Count > 0) return AiRequestSanitizationResult.Fail(errors);

            var sanitized = new JsonObject
            {
                ["schemaVersion"] = SchemaVersion,
                ["id"] = id,
                ["intent"] = intent,
                ["message"] = message,
                ["context"] = SanitizeContext(context)
            };

            return AiRequestSanitizationResult.Success(new SanitizedAiRequest(
                id,
                intent,
                message,
                sanitized.ToJsonString(JsonOptions)));
        }
        catch (JsonException)
        {
            return AiRequestSanitizationResult.Fail("AI request payload is not valid JSON.");
        }
    }

    public static string NormalizeIntent(string? value, string fallback = "create-variable")
        => value switch
        {
            "clone-variable" => "clone-variable",
            "map-io" => "map-io",
            "create-flow" => "create-flow",
            "create-structure" => "create-structure",
            _ => fallback
        };

    public static string? NormalizeFixtureName(string? value)
        => value switch
        {
            "malformed-json" => "malformed-json",
            "wrong-proposal-shape" => "wrong-proposal-shape",
            "partial-json" => "partial-json",
            "stream-timeout" => "stream-timeout",
            "stream-cancel" => "stream-cancel",
            "stream-error" => "stream-error",
            _ => null
        };

    private static JsonObject SanitizeContext(JsonElement context)
    {
        var output = new JsonObject();
        CopyAllowed(output, context, "variables");
        CopyAllowed(output, context, "units");
        CopyAllowed(output, context, "diagrams");
        CopyAllowed(output, context, "steps");
        CopyAllowed(output, context, "ioMapping");
        CopyAllowed(output, context, "flows");
        CopyAllowed(output, context, "selection");
        CopyAllowed(output, context, "existingStructures");
        return output;
    }

    private static void CopyAllowed(JsonObject output, JsonElement source, string propertyName)
    {
        if (!source.TryGetProperty(propertyName, out var property)) return;

        var node = JsonNode.Parse(property.GetRawText());
        var sanitized = SanitizeContextNode(node);
        if (sanitized is not null) output[propertyName] = sanitized;
    }

    private static JsonNode? SanitizeContextNode(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            var output = new JsonObject();
            foreach (var property in obj)
            {
                if (SensitiveContextKeys.Contains(property.Key)) continue;
                var child = SanitizeContextNode(property.Value?.DeepClone());
                if (child is not null) output[property.Key] = child;
            }

            return output;
        }

        if (node is JsonArray array)
        {
            var output = new JsonArray();
            foreach (var item in array)
            {
                var child = SanitizeContextNode(item?.DeepClone());
                if (child is not null) output.Add(child);
            }

            return output;
        }

        if (node is JsonValue value && value.TryGetValue<string>(out var text))
        {
            return LooksSensitiveText(text) ? null : JsonValue.Create(TrimToLimit(text, 1000));
        }

        return node;
    }

    private static bool LooksSensitiveText(string value)
        => value.Contains("C:\\", StringComparison.OrdinalIgnoreCase)
            || value.Contains("\\\\", StringComparison.Ordinal)
            || value.Contains("templates\\", StringComparison.OrdinalIgnoreCase)
            || value.Contains("sk-test-secret", StringComparison.OrdinalIgnoreCase)
            || value.Contains("AIza", StringComparison.OrdinalIgnoreCase);

    private static string GetString(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;

    private static string TrimToLimit(string value, int maxLength)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}

public sealed record SanitizedAiRequest(string Id, string Intent, string Message, string Json);

public sealed record AiRequestSanitizationResult(bool Ok, SanitizedAiRequest? Request, IReadOnlyList<string> Errors)
{
    public static AiRequestSanitizationResult Success(SanitizedAiRequest request) => new(true, request, Array.Empty<string>());

    public static AiRequestSanitizationResult Fail(string error) => new(false, null, new[] { error });

    public static AiRequestSanitizationResult Fail(IReadOnlyList<string> errors) => new(false, null, errors);
}

