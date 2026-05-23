using Microsoft.Win32;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using Forms = System.Windows.Forms;

namespace GrafcetStudio.App.Services;

public class FileService : IFileService
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

    public async Task SaveProjectAsync(string projectJson)
    {
        string? path = null;
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "GRAFCET Project|*.grafcet|All Files|*.*", DefaultExt = ".grafcet" };
            if (dlg.ShowDialog() == true) path = dlg.FileName;
        });
        if (string.IsNullOrWhiteSpace(path)) return;
        await File.WriteAllTextAsync(path, projectJson, Utf8NoBom);
    }

    public async Task<string?> OpenProjectAsync()
    {
        string? path = null;
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "GRAFCET Project|*.grafcet|All Files|*.*" };
            if (dlg.ShowDialog() == true) path = dlg.FileName;
        });
        if (string.IsNullOrWhiteSpace(path)) return null;
        return await File.ReadAllTextAsync(path, Utf8NoBom);
    }

    public async Task ExportCodeAsync(string code, string platform)
    {
        var (ext, filter) = MapPlatform(platform);
        string? path = null;
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var dlg = new Microsoft.Win32.SaveFileDialog { Filter = filter, DefaultExt = ext };
            if (dlg.ShowDialog() == true) path = dlg.FileName;
        });
        if (string.IsNullOrWhiteSpace(path)) return;
        await File.WriteAllTextAsync(path, code, Utf8NoBom);
    }

    public Task<string?> BrowseDeviceLibraryPathAsync()
    {
        var dispatcher = System.Windows.Application.Current.Dispatcher;

        return dispatcher.InvokeAsync(() =>
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Device Library JSON|*.json|All Files|*.*",
                DefaultExt = ".json",
                Title = "Select Device Library"
            };

            var owner = System.Windows.Application.Current.MainWindow;
            return dlg.ShowDialog(owner) == true ? dlg.FileName : null;
        }).Task;
    }

    public Task<string?> BrowseTemplateRootPathAsync()
    {
        return BrowseFolderAsync("Select template root folder");
    }

    public Task<string?> BrowseOutputRootPathAsync()
    {
        return BrowseFolderAsync("Select output folder");
    }

    private static Task<string?> BrowseFolderAsync(string description)
    {
        var dispatcher = System.Windows.Application.Current.Dispatcher;

        return dispatcher.InvokeAsync(() =>
        {
            using var dlg = new Forms.FolderBrowserDialog
            {
                Description = description,
                UseDescriptionForTitle = true
            };

            var ownerWindow = System.Windows.Application.Current.MainWindow;
            var ownerHandle = new WindowInteropHelper(ownerWindow).Handle;
            var owner = new Win32Window(ownerHandle);
            return dlg.ShowDialog(owner) == Forms.DialogResult.OK ? dlg.SelectedPath : null;
        }).Task;
    }

    private sealed class Win32Window : Forms.IWin32Window
    {
        public Win32Window(IntPtr handle)
        {
            Handle = handle;
        }

        public IntPtr Handle { get; }
    }

    private static (string Ext, string Filter) MapPlatform(string platform) => platform?.ToLowerInvariant() switch
    {
        "kv-5500" => (".mnm", "KV Mnemonic|*.mnm"),
        "kv-8000" => (".mnm", "KV Mnemonic|*.mnm"),
        "melsec" => (".gxw", "MELSEC Program|*.gxw"),
        "omron" => (".cxp", "Omron Program|*.cxp"),
        "siemens" => (".awl", "Siemens STL|*.awl"),
        "twincat-st" => (".st", "Structured Text|*.st"),
        _ => (".txt", "Text File|*.txt")
    };
}
