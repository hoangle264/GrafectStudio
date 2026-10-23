using GrafcetStudio.App.Services.Ai;
using System.Net;
using System.Net.Http;
using System.Text;
using Xunit;

namespace GrafcetStudio.App.Tests;

public class GeminiAiCompletionServiceTests
{
    private const string Key = "test-secret-key";

    private static AiCompletionRequest Request()
        => new(new SanitizedAiRequest("r1", "classify-intent", "hi", "{\"systemPrompt\":\"s\",\"userPrompt\":\"u\"}"));

    [Fact]
    public async Task CompleteAsync_SendsKeyInHeader_NotInUrl()
    {
        var handler = new CapturingHandler("{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"ok\"}]}}]}");
        var service = new GeminiAiCompletionService(new HttpClient(handler), Key, "gemini-test");

        var result = await service.CompleteAsync(Request());

        Assert.True(result.Ok);
        Assert.DoesNotContain(Key, handler.Urls.Single());
        Assert.Equal(Key, handler.KeyHeaders.Single());
    }

    [Fact]
    public async Task StreamAsync_SendsKeyInHeader_NotInUrl()
    {
        var handler = new CapturingHandler("data: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"ok\"}]}}]}\n\n");
        var service = new GeminiAiCompletionService(new HttpClient(handler), Key, "gemini-test");

        await foreach (var _ in service.StreamAsync(Request())) { }

        var url = handler.Urls.Single();
        Assert.DoesNotContain(Key, url);
        Assert.Contains("alt=sse", url);
        Assert.Equal(Key, handler.KeyHeaders.Single());
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly string _body;
        public List<string> Urls { get; } = new();
        public List<string> KeyHeaders { get; } = new();

        public CapturingHandler(string body) => _body = body;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Urls.Add(request.RequestUri!.ToString());
            KeyHeaders.Add(request.Headers.TryGetValues("x-goog-api-key", out var values) ? values.Single() : string.Empty);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(_body, Encoding.UTF8, "application/json") });
        }
    }
}
