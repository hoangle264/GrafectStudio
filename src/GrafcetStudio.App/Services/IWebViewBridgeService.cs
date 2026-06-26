using GrafcetStudio.Domain.Models;
using System.Threading.Tasks;

namespace GrafcetStudio.App.Services;

public interface IWebViewBridgeService
{
    void Init(Microsoft.Web.WebView2.Wpf.WebView2 webView);
    Task SendGeneratedCodeAsync(CodegenOutput output);
    Task SendAiChunkAsync(string chunk);
    Task SendAiResponseAsync(string rawText);
    Task SendAiStreamEventAsync(string kind, string? text = null, bool done = false);
    Task SendErrorAsync(string source, string message);
    Task LoadProjectDataAsync(string json);
    Task UpdateDiagramStateAsync(string actionsJson);
    Task SendCodegenPathAsync(string target, string path);
    Task SendSavedPathsAsync(string deviceLibraryPath, string templatePath, string outputPath);
    Task SendTemplateFileAsync(string requestId, string relativePath, string content);
}

