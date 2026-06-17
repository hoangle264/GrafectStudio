using GrafcetStudio.App.Events;
using GrafcetStudio.App.Services.Ai;
using Prism.Events;
using System.Text.Json;

namespace GrafcetStudio.App.Services;

public class AiRequestOrchestrator
{
    private readonly IWebViewBridgeService _webViewBridgeService;
    private readonly IAiCompletionService _aiCompletionService;

    public AiRequestOrchestrator(IEventAggregator eventAggregator, IWebViewBridgeService webViewBridgeService, IAiCompletionService aiCompletionService)
    {
        _webViewBridgeService = webViewBridgeService;
        _aiCompletionService = aiCompletionService;
        eventAggregator.GetEvent<AiRequestedEvent>().Subscribe(async payload => await HandleAiRequestAsync(payload));
    }

    private async Task HandleAiRequestAsync(AiRequestPayload payload)
    {
        var sanitized = AiContractGuard.SanitizeRequestJson(payload.RequestJson);
        if (!sanitized.Ok || sanitized.Request is null)
        {
            await SendErrorProposalAsync(payload, sanitized.Errors);
            return;
        }

        var fixtureName = AiContractGuard.NormalizeFixtureName(payload.FixtureName);
        var result = await _aiCompletionService.CompleteAsync(new AiCompletionRequest(sanitized.Request, fixtureName));

        if (!result.Ok)
        {
            await SendErrorProposalAsync(payload, result.Errors);
            return;
        }

        await _webViewBridgeService.SendAiResponseAsync(result.RawText);
    }

    private async Task SendErrorProposalAsync(AiRequestPayload payload, IReadOnlyList<string> errors)
    {
        var intent = AiContractGuard.NormalizeIntent(payload.Type);
        var requestId = TryReadRequestId(payload.RequestJson);
        var rawText = JsonSerializer.Serialize(new
        {
            schemaVersion = AiContractGuard.SchemaVersion,
            id = "ai-prop-host-error-" + Guid.NewGuid().ToString("N")[..8],
            intent,
            status = "invalid",
            requestId = string.IsNullOrWhiteSpace(requestId) ? null : requestId,
            summary = "Host AI service could not produce a valid proposal.",
            warnings = Array.Empty<string>(),
            errors,

            data = BuildErrorProposalData(intent)

        });

        await _webViewBridgeService.SendAiResponseAsync(rawText);
    }

    private static object BuildErrorProposalData(string intent)
        => intent switch
        {
            "clone-variable" => (object)new { source = new { label = string.Empty }, variable = EmptyVariable() },
            "map-io" => (object)new { entries = Array.Empty<object>() },
            "create-flow" => (object)new { flow = new { id = string.Empty, name = string.Empty, type = string.Empty, steps = Array.Empty<object>(), transitions = Array.Empty<object>(), connections = Array.Empty<object>() } },
            _ => (object)new { bucket = "user", variable = EmptyVariable() }
        };

    private static object EmptyVariable() => new
    {
        label = string.Empty,
        format = string.Empty,
        address = string.Empty,
        kind = string.Empty,
        dataType = string.Empty,
        comment = string.Empty,
        source = string.Empty
    };

    private static string TryReadRequestId(string requestJson)
    {
        if (string.IsNullOrWhiteSpace(requestJson)) return string.Empty;

        try
        {
            using var doc = JsonDocument.Parse(requestJson);
            return doc.RootElement.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String
                ? id.GetString() ?? string.Empty
                : string.Empty;
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }
}


