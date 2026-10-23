using System.Text;
using System.Text.Json;

namespace GrafcetStudio.App.Services.Ai;

public sealed record AiIntentChoice(string? Intent, string Reason, string Question);

/// <summary>
/// Picks which proposal intent fits a user message. Only the message the user typed is sent; no project data.
/// </summary>
public static class AiIntentClassifier
{
    public const int MaxMessageChars = 4000;

    private static readonly (string Intent, string Description)[] IntentDescriptions =
    {
        ("create-variable", "add one new variable/tag (name, data type, address) to the project"),
        ("clone-variable", "duplicate an existing variable into several copies with new names/addresses"),
        ("map-io", "assign physical PLC I/O points to application variables in the IO mapping table"),
        ("create-flow", "create a Grafcet sequence (steps, transitions) from a described machine sequence"),
        ("create-structure", "define a new device type/structure with its signals (e.g. a cylinder with extend/retract)")
    };

    public static IReadOnlyList<string> Intents => IntentDescriptions.Select(d => d.Intent).ToArray();

    public static SanitizedAiRequest BuildRequest(string requestId, string message)
    {
        var trimmed = (message ?? string.Empty).Trim();
        if (trimmed.Length > MaxMessageChars) trimmed = trimmed[..MaxMessageChars];

        var system = new StringBuilder();
        system.AppendLine("You route requests inside GrafcetStudio, a PLC/robot automation editor.");
        system.AppendLine("Choose the ONE intent that fits the user's request:");
        foreach (var (intent, description) in IntentDescriptions) system.AppendLine($"- {intent}: {description}");
        system.AppendLine("If none fits, or the request is too vague to choose, use intent \"unknown\" and ask one short clarifying question in the user's language.");
        system.AppendLine("Respond with raw JSON only: {\"intent\": string, \"reason\": string, \"question\": string}. Keep reason under 20 words, in the user's language.");

        var json = JsonSerializer.Serialize(new { systemPrompt = system.ToString(), userPrompt = trimmed });
        return new SanitizedAiRequest(requestId, "classify-intent", trimmed, json);
    }

    public static AiIntentChoice Parse(string rawText)
    {
        var text = rawText ?? string.Empty;
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start) return new AiIntentChoice(null, string.Empty, string.Empty);

        try
        {
            using var doc = JsonDocument.Parse(text[start..(end + 1)]);
            var root = doc.RootElement;
            var intent = ReadString(root, "intent");
            return new AiIntentChoice(
                Intents.Contains(intent) ? intent : null,
                ReadString(root, "reason"),
                ReadString(root, "question"));
        }
        catch (JsonException)
        {
            return new AiIntentChoice(null, string.Empty, string.Empty);
        }
    }

    private static string ReadString(JsonElement root, string name)
        => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? (value.GetString() ?? string.Empty).Trim() : string.Empty;
}
