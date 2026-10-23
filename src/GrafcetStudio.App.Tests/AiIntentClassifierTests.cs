using GrafcetStudio.App.Services.Ai;
using System.Text.Json;
using Xunit;

namespace GrafcetStudio.App.Tests;

public class AiIntentClassifierTests
{
    [Fact]
    public void BuildRequest_SendsOnlyTheMessage_WithAllIntentsInSystemPrompt()
    {
        var request = AiIntentClassifier.BuildRequest("r1", "  gán IO cho xi lanh  ");

        using var doc = JsonDocument.Parse(request.Json);
        var system = doc.RootElement.GetProperty("systemPrompt").GetString()!;
        Assert.Equal("gán IO cho xi lanh", doc.RootElement.GetProperty("userPrompt").GetString());
        Assert.Equal(new[] { "systemPrompt", "userPrompt" }, doc.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.All(AiIntentClassifier.Intents, intent => Assert.Contains(intent, system));
    }

    [Fact]
    public void BuildRequest_TruncatesLongMessages()
    {
        var request = AiIntentClassifier.BuildRequest("r1", new string('x', AiIntentClassifier.MaxMessageChars + 50));
        Assert.Equal(AiIntentClassifier.MaxMessageChars, request.Message.Length);
    }

    [Fact]
    public void Parse_ReadsJsonWrappedInFences()
    {
        var choice = AiIntentClassifier.Parse("```json\n{\"intent\":\"map-io\",\"reason\":\"gán IO\",\"question\":\"\"}\n```");
        Assert.Equal("map-io", choice.Intent);
        Assert.Equal("gán IO", choice.Reason);
    }

    [Theory]
    [InlineData("{\"intent\":\"unknown\",\"reason\":\"\",\"question\":\"Bạn muốn làm gì?\"}")]
    [InlineData("{\"intent\":\"delete-project\"}")]
    [InlineData("not json")]
    public void Parse_ReturnsNoIntentForUnknownOrInvalidOutput(string raw)
    {
        Assert.Null(AiIntentClassifier.Parse(raw).Intent);
    }
}
