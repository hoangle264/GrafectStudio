using Microsoft.Web.WebView2.Wpf;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace GrafcetStudio.App.Services;

public class WebViewBridgeService : IWebViewBridgeService
{
    private WebView2? _webView;

    public void Init(WebView2 webView)
    {
        _webView = webView;
    }

    public async Task SendGeneratedCodeAsync(string code)
    {
        await ExecuteAsync($"receiveGeneratedCode({JsonSerializer.Serialize(code)});");
    }

    public async Task SendAiChunkAsync(string chunk)
    {
        await ExecuteAsync($"receiveAiChunk({JsonSerializer.Serialize(chunk)});");
    }

    public async Task SendErrorAsync(string source, string message)
    {
        var payload = JsonSerializer.Serialize(new { source, message });
        await ExecuteAsync($"receiveError({payload});");
    }

    public async Task LoadProjectDataAsync(string json)
    {
        await ExecuteAsync($"loadProjectData({JsonSerializer.Serialize(json)});");
    }

    public async Task UpdateDiagramStateAsync(string actionsJson)
    {
        await ExecuteAsync($"updateDiagramState({JsonSerializer.Serialize(actionsJson)});");
    }

    private async Task ExecuteAsync(string script)
    {
        if (_webView?.CoreWebView2 is null)
        {
            throw new InvalidOperationException("WebView2 is not initialized.");
        }

        await _webView.ExecuteScriptAsync(script);
    }
}
