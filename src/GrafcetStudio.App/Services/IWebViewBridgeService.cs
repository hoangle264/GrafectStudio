using System.Threading.Tasks;

namespace GrafcetStudio.App.Services;

public interface IWebViewBridgeService
{
    void Init(Microsoft.Web.WebView2.Wpf.WebView2 webView);
    Task SendGeneratedCodeAsync(string code);
    Task SendAiChunkAsync(string chunk);
    Task SendErrorAsync(string source, string message);
    Task LoadProjectDataAsync(string json);
    Task UpdateDiagramStateAsync(string actionsJson);
    Task SendCodegenPathAsync(string target, string path);
    Task SendSavedPathsAsync(string deviceLibraryPath, string templatePath, string outputPath);
}

