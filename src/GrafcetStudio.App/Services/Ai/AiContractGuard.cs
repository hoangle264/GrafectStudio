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
        "create-flow"
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
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
            _ => fallback
        };

    public static string? NormalizeFixtureName(string? value)
        => value switch
        {
            "malformed-json" => "malformed-json",
            "wrong-proposal-shape" => "wrong-proposal-shape",
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
        return output;
    }

    private static void CopyAllowed(JsonObject output, JsonElement source, string propertyName)
    {
        if (!source.TryGetProperty(propertyName, out var property)) return;

        var node = JsonNode.Parse(property.GetRawText());
        if (node is not null) output[propertyName] = node;
    }

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

