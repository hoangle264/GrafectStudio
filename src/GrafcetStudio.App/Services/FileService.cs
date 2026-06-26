using Microsoft.Win32;
using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using Forms = System.Windows.Forms;
using GrafcetStudio.Domain.Models;

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

    public async Task ExportCodeAsync(IReadOnlyList<CodegenFile> files, string platform)
    {
        if (files is null || files.Count == 0) return;

        var (ext, filter) = files.Count == 1 ? MapSingleFile(files[0].Path) : (".zip", "ZIP Archive|*.zip");
        string? path = null;
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var dlg = new Microsoft.Win32.SaveFileDialog { Filter = filter, DefaultExt = ext };
            if (dlg.ShowDialog() == true) path = dlg.FileName;
        });
        if (string.IsNullOrWhiteSpace(path)) return;

        if (files.Count == 1)
        {
            await File.WriteAllTextAsync(path, files[0].Content ?? string.Empty, Utf8NoBom);
            return;
        }

        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var file in files)
        {
            var entryName = NormalizeExportPath(file.Path, platform);
            var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
            await using var stream = entry.Open();
            await using var writer = new StreamWriter(stream, Utf8NoBom);
            await writer.WriteAsync(file.Content ?? string.Empty);
        }
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


    public async Task<string> ReadTemplateFileAsync(string rootPath, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new InvalidOperationException("Template root path is empty.");
        }

        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new InvalidOperationException("Template relative path is empty.");
        }

        var rootFullPath = Path.GetFullPath(rootPath.Trim());
        var safeRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fileFullPath = Path.GetFullPath(Path.Combine(rootFullPath, safeRelativePath));
        var rootWithSeparator = rootFullPath.EndsWith(Path.DirectorySeparatorChar) ? rootFullPath : rootFullPath + Path.DirectorySeparatorChar;

        if (!fileFullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase) && !string.Equals(fileFullPath, rootFullPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Template file path is outside the selected template root.");
        }

        if (!File.Exists(fileFullPath))
        {
            throw new FileNotFoundException("Template file not found.", fileFullPath);
        }

        return await File.ReadAllTextAsync(fileFullPath, Utf8NoBom);
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

    private static (string Ext, string Filter) MapSingleFile(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        if (string.IsNullOrWhiteSpace(ext)) ext = ".txt";
        return (ext, $"{ext.TrimStart('.').ToUpperInvariant()} File|*{ext}");
    }

    private static string NormalizeExportPath(string filePath, string platform)
    {
        var path = (filePath ?? string.Empty).Replace('\\', '/').TrimStart('/');
        if (string.IsNullOrWhiteSpace(path))
        {
            path = $"{(platform ?? string.Empty).Trim()}.st";
        }
        return path;
    }
}
