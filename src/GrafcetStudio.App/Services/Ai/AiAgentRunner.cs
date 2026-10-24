using System.Text.Json.Nodes;

namespace GrafcetStudio.App.Services.Ai;

public sealed record AiAgentToolCall(string CallId, string Name, JsonObject Args);

public sealed record AiAgentResult(bool Ok, string Error, int ModelTurns);

/// <summary>
/// Model-agnostic tool-use loop: ask the model, run every function call it makes, feed the results back,
/// until a tool result says <c>"done": true</c> (a proposal was accepted or a reply was shown) or the turn budget runs out.
/// The model's content is echoed back verbatim so provider metadata (e.g. thought signatures) is preserved.
/// </summary>
public static class AiAgentRunner
{
    public const int MaxModelTurns = 10;

    public static async Task<AiAgentResult> RunAsync(
        string userMessage,
        Func<JsonArray, CancellationToken, Task<JsonObject>> callModel,
        Func<AiAgentToolCall, CancellationToken, Task<JsonObject>> executeTool,
        CancellationToken cancellationToken = default,
        JsonArray? history = null)
    {
        var contents = (JsonArray?)history?.DeepClone() ?? new JsonArray();
        contents.Add(UserText(userMessage));

        for (var turn = 1; turn <= MaxModelTurns; turn++)
        {
            var modelContent = await callModel(contents, cancellationToken);
            var calls = ReadFunctionCalls(modelContent, turn);
            contents.Add(modelContent.DeepClone());

            if (calls.Count == 0)
            {
                contents.Add(UserText("Call exactly one tool. Finish with reply or submit_proposal."));
                continue;
            }

            var responses = new JsonArray();
            var done = false;
            foreach (var call in calls)
            {
                var result = await executeTool(call, cancellationToken);
                done |= result["done"]?.GetValue<bool>() == true;
                responses.Add(new JsonObject
                {
                    ["functionResponse"] = new JsonObject { ["name"] = call.Name, ["response"] = result.DeepClone() }
                });
            }

            if (done) return new AiAgentResult(true, string.Empty, turn);
            contents.Add(new JsonObject { ["role"] = "user", ["parts"] = responses });
        }

        return new AiAgentResult(false, $"The assistant did not finish within {MaxModelTurns} steps.", MaxModelTurns);
    }

    private static List<AiAgentToolCall> ReadFunctionCalls(JsonObject modelContent, int turn)
    {
        var calls = new List<AiAgentToolCall>();
        if (modelContent["parts"] is not JsonArray parts) return calls;
        foreach (var part in parts.OfType<JsonObject>())
        {
            if (part["functionCall"] is not JsonObject call) continue;
            var name = call["name"]?.GetValue<string>() ?? string.Empty;
            var args = call["args"] as JsonObject ?? new JsonObject();
            calls.Add(new AiAgentToolCall($"t{turn}-{calls.Count}", name, (JsonObject)args.DeepClone()));
        }
        return calls;
    }

    private static JsonObject UserText(string text)
        => new() { ["role"] = "user", ["parts"] = new JsonArray { new JsonObject { ["text"] = text } } };
}
