using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace GrafcetStudio.App.Services.Ai;

public class GeminiAiCompletionService : IAiCompletionService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;

    public GeminiAiCompletionService(HttpClient httpClient, string apiKey, string? model = null)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
        _model = string.IsNullOrWhiteSpace(model) ? "gemini-1.5-flash" : model.Trim();
    }

    public async Task<AiCompletionResult> CompleteAsync(AiCompletionRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            return AiCompletionResult.Failure("Gemini API key is not configured on the host.");
        }

        var payload = BuildGeminiPayload(request);

        var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(_model)}:generateContent?key={Uri.EscapeDataString(_apiKey)}";
        using var content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return AiCompletionResult.Failure($"Gemini request failed with HTTP {(int)response.StatusCode}.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var text = ExtractText(doc.RootElement);

        return string.IsNullOrWhiteSpace(text)
            ? AiCompletionResult.Failure("Gemini response did not include proposal text.")
            : AiCompletionResult.Success(text);
    }

    public async IAsyncEnumerable<AiStreamChunk> StreamAsync(AiCompletionRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            throw new InvalidOperationException("Gemini API key is not configured on the host.");
        }

        yield return AiStreamChunk.Status("Gemini streaming request started.");
        var payload = BuildGeminiPayload(request);
        var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(_model)}:streamGenerateContent?alt=sse&key={Uri.EscapeDataString(_apiKey)}";
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json")
        };

        using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Gemini streaming request failed with HTTP {(int)response.StatusCode}.");
        }

        var finalBuilder = new StringBuilder();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null) break;
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:", StringComparison.Ordinal)) continue;

            var data = line[5..].Trim();
            if (data == "[DONE]") break;

            var parseResult = TryExtractStreamText(data);
            if (parseResult.Malformed)
            {
                yield return AiStreamChunk.Status("Ignored malformed Gemini streaming metadata.");
                continue;
            }

            if (string.IsNullOrEmpty(parseResult.Text)) continue;
            finalBuilder.Append(parseResult.Text);
            yield return AiStreamChunk.Delta(parseResult.Text);
        }

        var finalText = finalBuilder.ToString();
        if (string.IsNullOrWhiteSpace(finalText))
        {
            throw new InvalidOperationException("Gemini stream ended without proposal text.");
        }

        yield return AiStreamChunk.Final(finalText);
    }


    private static object BuildGeminiPayload(AiCompletionRequest request)
    {
        var systemPrompt = AiPromptBuilder.BuildSystemPrompt(request.Request.Intent);
        var userPrompt = AiPromptBuilder.BuildUserPrompt(request.Request);
        return new
        {
            systemInstruction = new
            {
                parts = new[] { new { text = systemPrompt } }
            },
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = userPrompt } }
                }
            },
            generationConfig = new
            {
                temperature = 0.2,
                responseMimeType = "application/json"
            }
        };
    }


    private static (string Text, bool Malformed) TryExtractStreamText(string data)
    {
        try
        {
            using var doc = JsonDocument.Parse(data);
            return (ExtractText(doc.RootElement), false);
        }
        catch (JsonException)
        {
            return (string.Empty, true);
        }
    }
    private static string ExtractText(JsonElement root)
    {
        if (!root.TryGetProperty("candidates", out var candidates) || candidates.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var candidate in candidates.EnumerateArray())
        {
            if (!candidate.TryGetProperty("content", out var content)) continue;
            if (!content.TryGetProperty("parts", out var parts) || parts.ValueKind != JsonValueKind.Array) continue;

            foreach (var part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("text", out var textElement) && textElement.ValueKind == JsonValueKind.String)
                {
                    builder.Append(textElement.GetString());
                }
            }
        }

        return builder.ToString();
    }
}
