using Microsoft.Win32;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace GrafcetStudio.App.Services;

public class FileService : IFileService
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

    public async Task SaveProjectAsync(string projectJson)
    {
        string? path = null;
        Application.Current.Dispatcher.Invoke(() =>
        {
            var dlg = new SaveFileDialog { Filter = "GRAFCET Project|*.grafcet|All Files|*.*", DefaultExt = ".grafcet" };
            if (dlg.ShowDialog() == true) path = dlg.FileName;
        });
        if (string.IsNullOrWhiteSpace(path)) return;
        await File.WriteAllTextAsync(path, projectJson, Utf8NoBom);
    }

    public async Task<string?> OpenProjectAsync()
    {
        string? path = null;
        Application.Current.Dispatcher.Invoke(() =>
        {
            var dlg = new OpenFileDialog { Filter = "GRAFCET Project|*.grafcet|All Files|*.*" };
            if (dlg.ShowDialog() == true) path = dlg.FileName;
        });
        if (string.IsNullOrWhiteSpace(path)) return null;
        return await File.ReadAllTextAsync(path, Utf8NoBom);
    }

    public async Task ExportCodeAsync(string code, string platform)
    {
        var (ext, filter) = MapPlatform(platform);
        string? path = null;
        Application.Current.Dispatcher.Invoke(() =>
        {
            var dlg = new SaveFileDialog { Filter = filter, DefaultExt = ext };
            if (dlg.ShowDialog() == true) path = dlg.FileName;
        });
        if (string.IsNullOrWhiteSpace(path)) return;
        await File.WriteAllTextAsync(path, code, Utf8NoBom);
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
