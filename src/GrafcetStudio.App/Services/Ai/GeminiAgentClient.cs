using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;

namespace GrafcetStudio.App.Services.Ai;

/// <summary>One Gemini generateContent call with function declarations; returns the model's content object.</summary>
public class GeminiAgentClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;

    public GeminiAgentClient(HttpClient httpClient, string apiKey, string model)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
        _model = model.StartsWith("models/", StringComparison.OrdinalIgnoreCase) ? model[7..] : model;
    }

    public async Task<JsonObject> CallAsync(string systemPrompt, JsonArray contents, JsonArray tools, CancellationToken cancellationToken)
    {
        var payload = new JsonObject
        {
            ["systemInstruction"] = new JsonObject { ["parts"] = new JsonArray { new JsonObject { ["text"] = systemPrompt } } },
            ["contents"] = contents.DeepClone(),
            ["tools"] = tools.DeepClone(),
            // ANY forces a tool call every turn, so the assistant always ends through reply or submit_proposal.
            ["toolConfig"] = new JsonObject { ["functionCallingConfig"] = new JsonObject { ["mode"] = "ANY" } },
            ["generationConfig"] = new JsonObject { ["temperature"] = 0.2 }
        };

        var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(_model)}:generateContent";
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-goog-api-key", _apiKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var detail = body.Replace(Environment.NewLine, " ").Trim();
            if (detail.Length > 300) detail = detail[..300] + "...";
            throw new InvalidOperationException($"Gemini request failed with HTTP {(int)response.StatusCode}: {detail}");
        }

        var content = JsonNode.Parse(body)?["candidates"]?[0]?["content"] as JsonObject;
        return content is null
            ? throw new InvalidOperationException("Gemini response did not include content (it may have been blocked).")
            : (JsonObject)content.DeepClone();
    }
}
