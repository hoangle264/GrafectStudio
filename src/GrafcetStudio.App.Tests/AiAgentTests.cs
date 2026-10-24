using GrafcetStudio.App.Services.Ai;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using Xunit;

namespace GrafcetStudio.App.Tests;

public class AiAgentTests
{
    private static JsonObject ModelCall(string name, JsonObject? args = null, string? signature = null)
    {
        var part = new JsonObject { ["functionCall"] = new JsonObject { ["name"] = name, ["args"] = args ?? new JsonObject() } };
        if (signature != null) part["thoughtSignature"] = signature;
        return new JsonObject { ["role"] = "model", ["parts"] = new JsonArray { part } };
    }

    private static JsonArray History(params (string Role, string Text)[] entries)
        => new(entries.Select(e => (JsonNode)new JsonObject { ["role"] = e.Role, ["text"] = e.Text }).ToArray());

    private static string TurnText(JsonNode? turn) => turn!["parts"]![0]!["text"]!.GetValue<string>();

    [Fact]
    public void HistoryContents_EmptyOrInvalid_IsEmpty()
    {
        Assert.Empty(AiAgentPrompt.BuildHistoryContents(string.Empty));
        Assert.Empty(AiAgentPrompt.BuildHistoryContents("[]"));
        Assert.Empty(AiAgentPrompt.BuildHistoryContents("not json"));
    }

    [Fact]
    public void HistoryContents_KeepsTheLastEntries_AsAlternatingTurns()
    {
        var history = new JsonArray();
        for (var i = 1; i <= 10; i++)
            history.Add(new JsonObject { ["role"] = i % 2 == 1 ? "user" : "assistant", ["text"] = "msg" + i });

        var contents = AiAgentPrompt.BuildHistoryContents(history.ToJsonString());

        Assert.Equal(8, contents.Count);
        Assert.Equal("user", contents[0]!["role"]!.GetValue<string>());
        Assert.Equal("msg3", TurnText(contents[0]));
        Assert.Equal("model", contents[7]!["role"]!.GetValue<string>());
        Assert.Equal("msg10", TurnText(contents[7]));
    }

    [Fact]
    public void HistoryContents_MergesSameRole_StartsWithUser_EndsWithModel()
    {
        var contents = AiAgentPrompt.BuildHistoryContents(History(
            ("assistant", "welcome"), ("user", "a"), ("user", "b"), ("assistant", "c"), ("assistant", "[Proposal map-io: x - applied]"), ("user", "pending")).ToJsonString());

        Assert.Equal(2, contents.Count);
        Assert.Equal("a\n\nb", TurnText(contents[0]));
        Assert.Equal("c\n\n[Proposal map-io: x - applied]", TurnText(contents[1]));
    }

    [Fact]
    public void HistoryContents_TruncatesLongEntries()
    {
        var contents = AiAgentPrompt.BuildHistoryContents(History(("user", "q"), ("assistant", new string('x', 5000))).ToJsonString());

        Assert.Equal(AiAgentPrompt.MaxHistoryEntryChars, TurnText(contents[1]).Length);
    }

    [Fact]
    public async Task Runner_SendsHistoryBeforeTheCurrentRequest()
    {
        JsonArray? firstCall = null;
        var history = AiAgentPrompt.BuildHistoryContents(History(("user", "q1"), ("assistant", "a1")).ToJsonString());

        await AiAgentRunner.RunAsync("q2",
            (contents, _) => { firstCall ??= (JsonArray)contents.DeepClone(); return Task.FromResult(ModelCall("reply", new JsonObject { ["text"] = "a2" })); },
            (_, _) => Task.FromResult(new JsonObject { ["done"] = true }),
            history: history);

        Assert.Equal(new[] { "q1", "a1", "q2" }, firstCall!.Select(TurnText));
        Assert.Equal(2, history.Count);
    }

    [Fact]
    public async Task Runner_ExecutesToolsUntilDone_AndEchoesModelContentVerbatim()
    {
        var turns = new Queue<JsonObject>(new[]
        {
            ModelCall("list_variables", signature: "sig-1"),
            ModelCall("reply", new JsonObject { ["text"] = "ok" })
        });
        var seenContents = new List<JsonArray>();
        var executed = new List<string>();

        var result = await AiAgentRunner.RunAsync(
            "how many variables?",
            (contents, _) => { seenContents.Add((JsonArray)contents.DeepClone()); return Task.FromResult(turns.Dequeue()); },
            (call, _) =>
            {
                executed.Add(call.Name);
                return Task.FromResult(call.Name == "reply" ? new JsonObject { ["done"] = true } : new JsonObject { ["data"] = new JsonArray("A", "B") });
            });

        Assert.True(result.Ok);
        Assert.Equal(2, result.ModelTurns);
        Assert.Equal(new[] { "list_variables", "reply" }, executed);
        // Second model call carries: user message, model's call (with its signature), and the function response.
        var second = seenContents[1];
        Assert.Equal(3, second.Count);
        Assert.Equal("sig-1", second[1]!["parts"]![0]!["thoughtSignature"]!.GetValue<string>());
        Assert.Equal("list_variables", second[2]!["parts"]![0]!["functionResponse"]!["name"]!.GetValue<string>());
    }

    [Fact]
    public async Task Runner_StopsAfterTurnBudget()
    {
        var calls = 0;
        var result = await AiAgentRunner.RunAsync(
            "loop",
            (_, _) => { calls++; return Task.FromResult(ModelCall("list_diagrams")); },
            (_, _) => Task.FromResult(new JsonObject { ["data"] = new JsonArray() }));

        Assert.False(result.Ok);
        Assert.Equal(AiAgentRunner.MaxModelTurns, calls);
    }

    [Fact]
    public async Task Runner_NudgesWhenModelReturnsTextInsteadOfToolCall()
    {
        var turns = new Queue<JsonObject>(new[]
        {
            new JsonObject { ["role"] = "model", ["parts"] = new JsonArray { new JsonObject { ["text"] = "hello" } } },
            ModelCall("reply", new JsonObject { ["text"] = "hi" })
        });
        JsonArray? last = null;

        var result = await AiAgentRunner.RunAsync("hi",
            (contents, _) => { last = contents; return Task.FromResult(turns.Dequeue()); },
            (_, _) => Task.FromResult(new JsonObject { ["done"] = true }));

        Assert.True(result.Ok);
        Assert.Contains("Call exactly one tool", last!.ToJsonString());
    }

    [Fact]
    public void Tools_DeclareReadToolsAndFinishers()
    {
        var names = AiAgentPrompt.BuildTools()[0]!["functionDeclarations"]!.AsArray().Select(t => t!["name"]!.GetValue<string>()).ToList();
        Assert.All(AiAgentPrompt.ReadToolScopes.Keys, name => Assert.Contains(name, names));
        Assert.Contains("submit_proposal", names);
        Assert.Contains("reply", names);
        Assert.Contains("create-flow", AiAgentPrompt.BuildSystemPrompt());
    }

    [Fact]
    public async Task Client_SendsKeyHeader_ForcesToolCalls_AndReturnsContent()
    {
        var handler = new CapturingHandler("{\"candidates\":[{\"content\":{\"role\":\"model\",\"parts\":[{\"functionCall\":{\"name\":\"reply\",\"args\":{\"text\":\"x\"}}}]}}]}");
        var client = new GeminiAgentClient(new HttpClient(handler), "secret", "models/gemini-test");

        var content = await client.CallAsync("sys", new JsonArray(), AiAgentPrompt.BuildTools(), CancellationToken.None);

        Assert.Equal("reply", content["parts"]![0]!["functionCall"]!["name"]!.GetValue<string>());
        Assert.DoesNotContain("secret", handler.Url);
        Assert.Contains("/v1beta/models/gemini-test:generateContent", handler.Url);
        Assert.Equal("secret", handler.Key);
        Assert.Equal("ANY", JsonNode.Parse(handler.Body)!["toolConfig"]!["functionCallingConfig"]!["mode"]!.GetValue<string>());
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly string _response;
        public string Url { get; private set; } = string.Empty;
        public string Key { get; private set; } = string.Empty;
        public string Body { get; private set; } = string.Empty;

        public CapturingHandler(string response) => _response = response;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Url = request.RequestUri!.ToString();
            Key = request.Headers.GetValues("x-goog-api-key").Single();
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(_response, Encoding.UTF8, "application/json") };
        }
    }
}
