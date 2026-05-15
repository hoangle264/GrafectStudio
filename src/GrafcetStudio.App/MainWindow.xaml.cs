using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
using System.Windows;

namespace GrafcetStudio.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await webView.EnsureCoreWebView2Async();
        var webPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "web"));
        webView.CoreWebView2.SetVirtualHostNameToFolderMapping("grafcet.local", webPath, CoreWebView2HostResourceAccessKind.Allow);
        webView.CoreWebView2.Navigate("https://grafcet.local/index.html");
    }
}
