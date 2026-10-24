using GrafcetStudio.App.Events;
using GrafcetStudio.App.Services.Ai;
using Prism.Events;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Text.Json.Nodes;

namespace GrafcetStudio.App.Services;

/// <summary>
/// Runs the assistant's tool-use loop on the host. Tools execute in the WebView (where project data and the
/// permission gate live); the host only relays calls and results and never sees more than the user allowed.
/// </summary>
public class AiAgentOrchestrator
{
    private static readonly HttpClient HttpClient = new();
    private static readonly TimeSpan ToolTimeout = TimeSpan.FromMinutes(10);

    private readonly IWebViewBridgeService _bridge;
    private readonly IAiCompletionService _aiCompletionService;
    private readonly AiKeyStore _keyStore;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonObject>> _pendingTools = new();

    public AiAgentOrchestrator(IEventAggregator eventAggregator, IWebViewBridgeService bridge, IAiCompletionService aiCompletionService, AiKeyStore keyStore)
    {
        _bridge = bridge;
        _aiCompletionService = aiCompletionService;
        _keyStore = keyStore;
        eventAggregator.GetEvent<AiAgentStartRequestedEvent>().Subscribe(async payload => await HandleStartAsync(payload));
        eventAggregator.GetEvent<AiAgentToolResultEvent>().Subscribe(HandleToolResult);
    }

    private async Task HandleStartAsync(AiAgentStartPayload payload)
    {
        var sessionId = payload.SessionId;
        if (_aiCompletionService is not DynamicAiCompletionService { IsLive: true })
        {
            await _bridge.SendAiAgentEventAsync(sessionId, "unavailable", null);
            return;
        }

        var (key, _) = _keyStore.ResolveKey();
        var model = _keyStore.ResolveModel();
        var client = new GeminiAgentClient(HttpClient, key!, model);
        var systemPrompt = AiAgentPrompt.BuildSystemPrompt();
        var tools = AiAgentPrompt.BuildTools();
        var message = AiAgentPrompt.TruncateMessage(payload.Message);
        var history = AiAgentPrompt.BuildHistoryContents(payload.HistoryJson);
        var turn = 0;

        try
        {
            var result = await AiAgentRunner.RunAsync(
                message,
                async (contents, ct) =>
                {
                    turn++;
                    AiAuditLog.Record(BuildAuditRecord(sessionId, turn, contents), "gemini", model);
                    await _bridge.SendAiAgentEventAsync(sessionId, "status", new JsonObject { ["text"] = "Thinking (step " + turn + ")..." });
                    return await client.CallAsync(systemPrompt, contents, tools, ct);
                },
                (call, ct) => ExecuteToolInWebViewAsync(sessionId, call, ct),
                history: history);

            await _bridge.SendAiAgentEventAsync(sessionId, result.Ok ? "end" : "error", result.Ok ? null : new JsonObject { ["text"] = result.Error });
        }
        catch (Exception error)
        {
            var text = error.Message.Replace(Environment.NewLine, " ").Trim();
            if (text.Length > 300) text = text[..300] + "...";
            await _bridge.SendAiAgentEventAsync(sessionId, "error", new JsonObject { ["text"] = "Assistant failed: " + text });
        }
        finally
        {
            foreach (var pendingKey in _pendingTools.Keys.Where(k => k.StartsWith(sessionId + ":", StringComparison.Ordinal)))
                if (_pendingTools.TryRemove(pendingKey, out var pending)) pending.TrySetCanceled();
        }
    }

    private async Task<JsonObject> ExecuteToolInWebViewAsync(string sessionId, AiAgentToolCall call, CancellationToken cancellationToken)
    {
        var pendingKey = sessionId + ":" + call.CallId;
        var completion = new TaskCompletionSource<JsonObject>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingTools[pendingKey] = completion;

        await _bridge.SendAiAgentEventAsync(sessionId, "tool_call", new JsonObject
        {
            ["callId"] = call.CallId,
            ["name"] = call.Name,
            ["args"] = call.Args.DeepClone()
        });

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ToolTimeout);
        try
        {
            return await completion.Task.WaitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            return new JsonObject { ["error"] = "The tool did not return in time (the user may not have answered the permission request)." };
        }
        finally
        {
            _pendingTools.TryRemove(pendingKey, out _);
        }
    }

    private void HandleToolResult(AiAgentToolResultPayload payload)
    {
        if (!_pendingTools.TryGetValue(payload.SessionId + ":" + payload.CallId, out var completion)) return;
        JsonObject result;
        try
        {
            result = JsonNode.Parse(payload.ResultJson) as JsonObject ?? new JsonObject { ["error"] = "Tool returned a non-object result." };
        }
        catch (System.Text.Json.JsonException)
        {
            result = new JsonObject { ["error"] = "Tool returned invalid JSON." };
        }
        completion.TrySetResult(result);
    }

    /// <summary>Audit shape only: which tool results are included in this model call and the payload size.</summary>
    private static SanitizedAiRequest BuildAuditRecord(string sessionId, int turn, JsonArray contents)
    {
        var shared = new JsonObject();
        foreach (var part in contents.OfType<JsonObject>().SelectMany(c => (c["parts"] as JsonArray ?? new JsonArray()).OfType<JsonObject>()))
        {
            if (part["functionResponse"] is JsonObject response && response["name"]?.GetValue<string>() is { } name && response["response"]?["denied"] is null)
                shared[name] = true;
        }
        var json = new JsonObject { ["context"] = shared, ["contents"] = contents.DeepClone() }.ToJsonString();
        return new SanitizedAiRequest($"agent-{sessionId}-{turn}", "agent", string.Empty, json);
    }
}
