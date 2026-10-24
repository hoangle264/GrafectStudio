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

    [Fact]
    public void BuildUserMessage_WithoutHistory_IsTheRequestOnly()
    {
        Assert.Equal("review flow", AiAgentPrompt.BuildUserMessage("review flow", string.Empty));
        Assert.Equal("review flow", AiAgentPrompt.BuildUserMessage("review flow", "[]"));
        Assert.Equal("review flow", AiAgentPrompt.BuildUserMessage("review flow", "not json"));
    }

    [Fact]
    public void BuildUserMessage_KeepsOnlyTheLastEntries_BeforeTheRequest()
    {
        var history = new JsonArray();
        for (var i = 1; i <= 10; i++)
            history.Add(new JsonObject { ["role"] = i % 2 == 1 ? "user" : "assistant", ["text"] = "msg" + i });

        var text = AiAgentPrompt.BuildUserMessage("and step 3?", history.ToJsonString());

        Assert.StartsWith("RECENT CONVERSATION", text);
        Assert.DoesNotContain("User: msg1" + Environment.NewLine, text);
        Assert.DoesNotContain("Assistant: msg2" + Environment.NewLine, text);
        Assert.Contains("User: msg3", text);
        Assert.Contains("Assistant: msg10", text);
        Assert.EndsWith("CURRENT REQUEST:" + Environment.NewLine + "and step 3?", text);
        Assert.True(text.IndexOf("msg10", StringComparison.Ordinal) < text.IndexOf("and step 3?", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildUserMessage_TruncatesLongEntries()
    {
        var history = new JsonArray { new JsonObject { ["role"] = "assistant", ["text"] = new string('x', 5000) } };

        var text = AiAgentPrompt.BuildUserMessage("ok", history.ToJsonString());

        Assert.Contains("Assistant: " + new string('x', AiAgentPrompt.MaxHistoryEntryChars) + Environment.NewLine, text);
        Assert.DoesNotContain(new string('x', AiAgentPrompt.MaxHistoryEntryChars + 1), text);
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
