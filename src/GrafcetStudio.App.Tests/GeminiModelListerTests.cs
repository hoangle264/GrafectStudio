using GrafcetStudio.App.Services.Ai;
using System.Text.Json;
using Xunit;

namespace GrafcetStudio.App.Tests;

public class GeminiModelListerTests
{
    private const string Json = """
    {"models":[
      {"name":"models/gemini-2.5-flash","supportedGenerationMethods":["generateContent","countTokens"]},
      {"name":"models/gemini-2.0-flash","supportedGenerationMethods":["generateContent"]},
      {"name":"models/gemini-2.5-pro","supportedGenerationMethods":["generateContent"]},
      {"name":"models/text-embedding-004","supportedGenerationMethods":["embedContent"]}
    ]}
    """;

    [Fact]
    public void ParseModels_KeepsOnlyGenerateContentModels_WithoutPrefix()
    {
        using var doc = JsonDocument.Parse(Json);
        var models = GeminiModelLister.ParseModels(doc.RootElement).ToList();
        Assert.Equal(new[] { "gemini-2.5-flash", "gemini-2.0-flash", "gemini-2.5-pro" }, models);
    }

    [Fact]
    public void Suggest_PicksNewestStableFlash()
    {
        using var doc = JsonDocument.Parse(Json);
        var models = GeminiModelLister.ParseModels(doc.RootElement).ToList();
        Assert.Equal("gemini-2.5-flash", GeminiModelLister.Suggest(models));
    }

    [Fact]
    public void Suggest_FallsBackToFirstWhenNoFlash()
    {
        Assert.Equal("a-model", GeminiModelLister.Suggest(new[] { "a-model", "b-model" }));
    }
}
