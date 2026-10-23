using System.IO;
using System.Text;
using System.Text.Json;

namespace GrafcetStudio.App.Services.Ai;

/// <summary>
/// Local record of every request that actually left the machine: when, which intent, which context keys, how large.
/// The content itself is not written, only its shape.
/// </summary>
public static class AiAuditLog
{
    private static readonly object Sync = new();

    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GrafcetStudio", "ai_audit.log");

    public static void Record(SanitizedAiRequest request, string provider, string model, string? path = null)
    {
        try
        {
            var line = JsonSerializer.Serialize(new
            {
                time = DateTimeOffset.Now.ToString("O"),
                provider,
                model,
                requestId = request.Id,
                intent = request.Intent,
                contextKeys = ReadContextKeys(request.Json),
                bytes = Encoding.UTF8.GetByteCount(request.Json)
            });
            var target = path ?? DefaultPath;
            lock (Sync)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.AppendAllText(target, line + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string[] ReadContextKeys(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("context", out var context) && context.ValueKind == JsonValueKind.Object
                ? context.EnumerateObject().Select(p => p.Name).ToArray()
                : Array.Empty<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }
}
