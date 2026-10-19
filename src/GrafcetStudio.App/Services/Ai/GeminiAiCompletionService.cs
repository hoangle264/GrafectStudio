using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GrafcetStudio.App.Services.Ai;

public class GeminiAiCompletionService : IAiCompletionService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string[] _apiVersions;

    public GeminiAiCompletionService(HttpClient httpClient, string apiKey, string? model = null)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
        _model = NormalizeModelName(model);
        _apiVersions = ResolveApiVersions();
    }

    public async Task<AiCompletionResult> CompleteAsync(AiCompletionRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            return AiCompletionResult.Failure("Gemini API key is not configured on the host.");
        }

        var lastFailure = string.Empty;

        foreach (var apiVersion in _apiVersions)
        {
            var payload = BuildGeminiPayload(request, apiVersion);
            var serializedPayload = JsonSerializer.Serialize(payload, JsonOptions);
            var endpoint = BuildEndpoint(apiVersion, "generateContent");
            GeminiDebugLogger.LogRequest("generateContent", apiVersion, _model, endpoint, serializedPayload);
            using var content = new StringContent(serializedPayload, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                lastFailure = await BuildHttpFailureMessageAsync(response, "Gemini request", cancellationToken);
                GeminiDebugLogger.LogResponse("generateContent", apiVersion, (int)response.StatusCode, lastFailure);
                if (ShouldTryNextApiVersion(response)) continue;
                return AiCompletionResult.Failure(lastFailure);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var text = ExtractText(doc.RootElement);
            GeminiDebugLogger.LogResponse("generateContent", apiVersion, (int)response.StatusCode, "OK textLength=" + text.Length + " rawText=" + GeminiDebugLogger.Truncate(text, 4000));

            return string.IsNullOrWhiteSpace(text)
                ? AiCompletionResult.Failure("Gemini response did not include proposal text.")
                : AiCompletionResult.Success(text);
        }

        return AiCompletionResult.Failure(string.IsNullOrWhiteSpace(lastFailure) ? "Gemini request failed for all configured API versions." : lastFailure);
    }

    public async IAsyncEnumerable<AiStreamChunk> StreamAsync(AiCompletionRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            throw new InvalidOperationException("Gemini API key is not configured on the host.");
        }

        yield return AiStreamChunk.Status("Gemini streaming request started.");
        var lastFailure = string.Empty;

        foreach (var apiVersion in _apiVersions)
        {
            yield return AiStreamChunk.Status("Gemini streaming request using " + apiVersion + ".");
            var payload = BuildGeminiPayload(request, apiVersion);
            var serializedPayload = JsonSerializer.Serialize(payload, JsonOptions);
            var endpoint = BuildEndpoint(apiVersion, "streamGenerateContent", "alt=sse");
            GeminiDebugLogger.LogRequest("streamGenerateContent", apiVersion, _model, endpoint, serializedPayload);
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(serializedPayload, Encoding.UTF8, "application/json")
            };

            using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                lastFailure = await BuildHttpFailureMessageAsync(response, "Gemini streaming request", cancellationToken);
                GeminiDebugLogger.LogResponse("streamGenerateContent", apiVersion, (int)response.StatusCode, lastFailure);
                if (ShouldTryNextApiVersion(response)) continue;
                throw new InvalidOperationException(lastFailure);
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
            GeminiDebugLogger.LogResponse("streamGenerateContent", apiVersion, (int)response.StatusCode, "OK textLength=" + finalText.Length + " rawText=" + GeminiDebugLogger.Truncate(finalText, 4000));
            if (string.IsNullOrWhiteSpace(finalText))
            {
                throw new InvalidOperationException("Gemini stream ended without proposal text.");
            }

            yield return AiStreamChunk.Final(finalText);
            yield break;
        }

        throw new InvalidOperationException(string.IsNullOrWhiteSpace(lastFailure) ? "Gemini streaming request failed for all configured API versions." : lastFailure);
    }


    private string BuildEndpoint(string apiVersion, string method, string? extraQuery = null)
    {
        var query = string.IsNullOrWhiteSpace(extraQuery)
            ? "key=" + Uri.EscapeDataString(_apiKey)
            : extraQuery + "&key=" + Uri.EscapeDataString(_apiKey);
        return $"https://generativelanguage.googleapis.com/{apiVersion}/models/{Uri.EscapeDataString(_model)}:{method}?{query}";
    }

    private static string NormalizeModelName(string? model)
    {
        var value = string.IsNullOrWhiteSpace(model) ? "gemini-2.0-flash" : model.Trim();
        return value.StartsWith("models/", StringComparison.OrdinalIgnoreCase) ? value[7..] : value;
    }

    private static string[] ResolveApiVersions()
    {
        var configured = Environment.GetEnvironmentVariable("GRAFCETSTUDIO_GEMINI_API_VERSION")?.Trim();
        if (!string.IsNullOrWhiteSpace(configured)) return new[] { configured.TrimStart('/') };
        return new[] { "v1", "v1beta" };
    }

    private static bool ShouldTryNextApiVersion(HttpResponseMessage response)
        => (int)response.StatusCode == 404;

    private static async Task<string> BuildHttpFailureMessageAsync(HttpResponseMessage response, string operation, CancellationToken cancellationToken)
    {
        var detail = string.Empty;
        try
        {
            detail = await response.Content.ReadAsStringAsync(cancellationToken);
            detail = detail.Replace(Environment.NewLine, " ").Trim();
            if (detail.Length > 300) detail = detail[..300] + "...";
        }
        catch
        {
            detail = string.Empty;
        }

        return string.IsNullOrWhiteSpace(detail)
            ? $"{operation} failed with HTTP {(int)response.StatusCode}."
            : $"{operation} failed with HTTP {(int)response.StatusCode}: {detail}";
    }

    private static object BuildGeminiPayload(AiCompletionRequest request, string apiVersion)
    {
        // Cố gắng lấy systemPrompt / userPrompt đã được build sẵn bởi RobotPromptBuilder
        // (được serialize vào field Json của SanitizedAiRequest tại AbbRapidGenerator).
        // Nếu không parse được thì fallback về AiPromptBuilder chung cho các AI request thông thường.
        string systemPrompt;
        string userPrompt;

        try
        {
            using var doc = JsonDocument.Parse(request.Request.Json);
            var root = doc.RootElement;
            var hasSys  = root.TryGetProperty("systemPrompt", out var sysProp)  && sysProp.ValueKind == JsonValueKind.String;
            var hasUser = root.TryGetProperty("userPrompt",   out var userProp) && userProp.ValueKind == JsonValueKind.String;

            if (hasSys && hasUser)
            {
                systemPrompt = sysProp.GetString()!;
                userPrompt   = userProp.GetString()!;
            }
            else
            {
                // Request thông thường (AI chat, proposal, v.v.) — dùng prompt builder chung
                systemPrompt = AiPromptBuilder.BuildSystemPrompt(request.Request.Intent);
                userPrompt   = AiPromptBuilder.BuildUserPrompt(request.Request);
            }
        }
        catch
        {
            systemPrompt = AiPromptBuilder.BuildSystemPrompt(request.Request.Intent);
            userPrompt   = AiPromptBuilder.BuildUserPrompt(request.Request);
        }

        // Gemini API hỗ trợ systemInstruction riêng biệt (không merge vào user turn).
        // Chỉ dùng systemInstruction khi có system prompt thực sự.
        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            return new
            {
                systemInstruction = new
                {
                    parts = new[] { new { text = systemPrompt } }
                },
                contents = new[]
                {
                    new { role = "user", parts = new[] { new { text = userPrompt } } }
                },
                generationConfig = new { temperature = 0.2 }
            };
        }

        // Fallback: gộp system + user prompt vào một turn (hành vi cũ)
        var combinedPrompt = systemPrompt + Environment.NewLine + Environment.NewLine + userPrompt;
        return new
        {
            contents = new[]
            {
                new { role = "user", parts = new[] { new { text = combinedPrompt } } }
            },
            generationConfig = new { temperature = 0.2 }
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


internal static class GeminiDebugLogger
{
    private static readonly object Sync = new();

    public static void LogRequest(string operation, string apiVersion, string model, string endpoint, string payload)
        => Write("REQUEST", operation, apiVersion, model, new[]
        {
            "endpoint=" + ScrubEndpoint(endpoint),
            "payload=" + ScrubPayload(payload)
        });

    public static void LogResponse(string operation, string apiVersion, int statusCode, string detail)
        => Write("RESPONSE", operation, apiVersion, string.Empty, new[]
        {
            "status=" + statusCode,
            "detail=" + detail
        });

    private static void Write(string kind, string operation, string apiVersion, string model, IReadOnlyList<string> lines)
    {
        try
        {
            var builder = new StringBuilder();
            builder.AppendLine("[" + DateTimeOffset.Now.ToString("O") + "] Gemini " + kind);
            builder.AppendLine("operation=" + operation);
            builder.AppendLine("apiVersion=" + apiVersion);
            if (!string.IsNullOrWhiteSpace(model)) builder.AppendLine("model=" + model);
            foreach (var line in lines) builder.AppendLine(line);
            builder.AppendLine();

            var text = builder.ToString();
            lock (Sync)
            {
                File.AppendAllText(Path.Combine(Environment.CurrentDirectory, "debug.log"), text, Encoding.UTF8);
                var basePath = Path.Combine(AppContext.BaseDirectory, "debug.log");
                var currentPath = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "debug.log"));
                if (!string.Equals(Path.GetFullPath(basePath), currentPath, StringComparison.OrdinalIgnoreCase))
                {
                    File.AppendAllText(basePath, text, Encoding.UTF8);
                }
            }
        }
        catch
        {
        }
    }

    private static string ScrubEndpoint(string endpoint)
    {
        var marker = "key=";
        var index = endpoint.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0) return endpoint;

        var end = endpoint.IndexOf('&', index + marker.Length);
        return end < 0
            ? endpoint[..(index + marker.Length)] + "<redacted>"
            : endpoint[..(index + marker.Length)] + "<redacted>" + endpoint[end..];
    }

    private static string ScrubPayload(string payload)
    {
        try
        {
            var node = JsonNode.Parse(payload);
            if (node is null) return payload;
            ScrubNode(node);
            return node.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        }
        catch
        {
            return payload.Length > 4000 ? payload[..4000] + "...<truncated>" : payload;
        }
    }

    public static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || maxLength <= 0) return string.Empty;
        if (value.Length <= maxLength) return value;
        return value[..maxLength] + "...";
    }

    private static void ScrubNode(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj.ToList())
            {
                if (property.Value is null) continue;
                if (string.Equals(property.Key, "text", StringComparison.OrdinalIgnoreCase))
                {
                    var value = property.Value.GetValue<string>();
                    obj[property.Key] = "<redacted length=" + value.Length + ">";
                    continue;
                }

                ScrubNode(property.Value);
            }
            return;
        }

        if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                if (item is not null) ScrubNode(item);
            }
        }
    }
}

