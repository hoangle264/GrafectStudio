using GrafcetStudio.App.Events;
using GrafcetStudio.App.Services;
using Microsoft.Web.WebView2.Core;
using Prism.Events;
using Prism.Ioc;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;

namespace GrafcetStudio.App;

public partial class MainWindow : Window
{
    private readonly IEventAggregator _eventAggregator;

    public MainWindow(IEventAggregator eventAggregator)
    {
        _eventAggregator = eventAggregator;
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await webView.EnsureCoreWebView2Async();

        var bridge = ((App)System.Windows.Application.Current).Container.Resolve<IWebViewBridgeService>();
        bridge.Init(webView);
        //webView.CoreWebView2.OpenDevToolsWindow();//test
        webView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

        var webPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "web"));
        if (webPath == null)
        {
            var tried = string.Join(Environment.NewLine, Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "web")));
            System.Windows.MessageBox.Show($"Required 'web' folder not found. Tried the following paths:\n{tried}", "Missing content", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        webView.CoreWebView2.SetVirtualHostNameToFolderMapping("grafcet.local", webPath, CoreWebView2HostResourceAccessKind.Allow);
        webView.CoreWebView2.NavigationCompleted += async (_, _) =>
        {
            var config = await ((App)System.Windows.Application.Current).Container.Resolve<ConfigService>().LoadAsync();
            await bridge.SendSavedPathsAsync(config.DeviceLibraryPath, config.TemplatePath, config.OutputPath);
        };
        webView.CoreWebView2.Navigate("https://grafcet.local/index.html");
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            return;
        }

        DragMove();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        using var doc = JsonDocument.Parse(e.WebMessageAsJson);
        #if DEBUG
               System.Diagnostics.Debug.WriteLine(JsonSerializer.Serialize(doc, JsonOptions));
        #endif
        if (!doc.RootElement.TryGetProperty("type", out var typeElement))
        {
            return;
        }

        var type = typeElement.GetString();
        var payload = doc.RootElement.TryGetProperty("payload", out var payloadElement) ? payloadElement : default;

        switch (type)
        {
            case "GENERATE_CODE":
            {
                var flowCount = GetOptionalArrayLength(payload, "flows");
                var variableCount = GetOptionalArrayLength(payload, "variables");
                Debug.WriteLine($"[Codegen] C# received payload counts: flows={flowCount}, variables={variableCount}");

                var message = new GenerateCodePayload
                {
                    DevPath=GetOptionalString(payload, "deviceLibraryPath"),
                    Platform = GetOptionalString(payload, "platform"),
                    TemplatePath = GetOptionalString(payload, "templateRootPath"),
                    OutputPath = GetOptionalString(payload, "outputPath"),
                    RawJson = payload.GetRawText()
                };
                _eventAggregator.GetEvent<GenerateCodeRequestedEvent>().Publish(message);
                break;
            }
            case "AI_REQUEST":
            {
                var message = new AiRequestPayload
                {
                    Type = payload.GetProperty("type").GetString() ?? string.Empty,
                    Prompt = payload.GetProperty("prompt").GetString() ?? string.Empty,
                    DiagramContext = payload.GetProperty("diagramContext").GetString() ?? string.Empty
                };
                _eventAggregator.GetEvent<AiRequestedEvent>().Publish(message);
                break;
            }
            case "SAVE_FILE":
            {
                var projectJson = payload.GetProperty("projectJson").GetString() ?? string.Empty;
                _eventAggregator.GetEvent<SaveFileRequestedEvent>().Publish(projectJson);
                break;
            }
            case "OPEN_FILE":
                _eventAggregator.GetEvent<OpenFileRequestedEvent>().Publish(new object());
                break;
            case "EXPORT_CODE":
            {
                var message = new ExportCodePayload
                {
                    Code = payload.GetProperty("code").GetString() ?? string.Empty,
                    Platform = payload.GetProperty("platform").GetString() ?? string.Empty
                };
                _eventAggregator.GetEvent<ExportCodeRequestedEvent>().Publish(message);
                break;
            }
            case "BROWSE_CODEGEN_PATH":
            {
                var message = new BrowseCodegenPathPayload
                {
                    Target = GetOptionalString(payload, "target")
                };
                _eventAggregator.GetEvent<BrowseCodegenPathRequestedEvent>().Publish(message);
                break;
            }
        }
    }

    private static string GetOptionalString(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;

    private static string GetOptionalString(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var value = GetOptionalString(element, propertyName);
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }

        return string.Empty;
    }

    private static int GetOptionalArrayLength(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Array
            ? property.GetArrayLength()
            : 0;
}


