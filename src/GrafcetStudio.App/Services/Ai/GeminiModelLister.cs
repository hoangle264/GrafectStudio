using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GrafcetStudio.App.Services.Ai;

public sealed record GeminiModelListResult(bool Ok, IReadOnlyList<string> Models, string Suggested, string Error);

/// <summary>Lists the Gemini models that support generateContent for a given API key.</summary>
public static partial class GeminiModelLister
{
    private const string Endpoint = "https://generativelanguage.googleapis.com/v1beta/models?pageSize=200";

    [GeneratedRegex(@"^gemini-(\d+(?:\.\d+)?)-flash$", RegexOptions.IgnoreCase)]
    private static partial Regex StableFlashPattern();

    public static async Task<GeminiModelListResult> ListAsync(HttpClient httpClient, string apiKey, CancellationToken cancellationToken = default)
    {
        var names = new List<string>();
        string? pageToken = null;
        try
        {
            do
            {
                var url = pageToken is null ? Endpoint : Endpoint + "&pageToken=" + Uri.EscapeDataString(pageToken);
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("x-goog-api-key", apiKey);
                using var response = await httpClient.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    return Failure(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized
                        ? "Gemini rejected the API key (HTTP " + (int)response.StatusCode + ")."
                        : "Gemini model list failed with HTTP " + (int)response.StatusCode + ".");
                }

                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                names.AddRange(ParseModels(doc.RootElement));
                pageToken = doc.RootElement.TryGetProperty("nextPageToken", out var next) && next.ValueKind == JsonValueKind.String
                    ? next.GetString()
                    : null;
            }
            while (!string.IsNullOrEmpty(pageToken));
        }
        catch (HttpRequestException ex)
        {
            return Failure("Could not reach Gemini: " + ex.Message);
        }
        catch (JsonException)
        {
            return Failure("Gemini returned an unreadable model list.");
        }

        var models = names.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        return models.Count == 0
            ? Failure("Gemini returned no models that support generateContent.")
            : new GeminiModelListResult(true, models, Suggest(models), string.Empty);
    }

    public static IEnumerable<string> ParseModels(JsonElement root)
    {
        if (!root.TryGetProperty("models", out var models) || models.ValueKind != JsonValueKind.Array) yield break;
        foreach (var model in models.EnumerateArray())
        {
            if (!model.TryGetProperty("name", out var nameElement) || nameElement.ValueKind != JsonValueKind.String) continue;
            if (!model.TryGetProperty("supportedGenerationMethods", out var methods) || methods.ValueKind != JsonValueKind.Array) continue;
            if (!methods.EnumerateArray().Any(m => m.ValueKind == JsonValueKind.String && m.GetString() == "generateContent")) continue;

            var name = nameElement.GetString() ?? string.Empty;
            yield return name.StartsWith("models/", StringComparison.OrdinalIgnoreCase) ? name[7..] : name;
        }
    }

    /// <summary>Prefers the newest stable "gemini-X-flash" model; otherwise the first listed.</summary>
    public static string Suggest(IReadOnlyList<string> models)
    {
        var best = models
            .Select(m => (Name: m, Match: StableFlashPattern().Match(m)))
            .Where(x => x.Match.Success)
            .OrderByDescending(x => double.Parse(x.Match.Groups[1].Value, CultureInfo.InvariantCulture))
            .Select(x => x.Name)
            .FirstOrDefault();
        return best ?? models[0];
    }

    private static GeminiModelListResult Failure(string error) => new(false, Array.Empty<string>(), string.Empty, error);
}
