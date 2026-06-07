using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace GrafcetStudio.App.Services;

public class AppConfig
{
    public string DeviceLibraryPath { get; set; } = string.Empty;
    public string TemplatePath { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
}

public class ConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private readonly string _configPath;

    public ConfigService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _configPath = Path.Combine(appData, "GrafcetStudio", "config.json");
    }

    public async Task<AppConfig> LoadAsync()
    {
        if (!File.Exists(_configPath)) return new AppConfig();

        try
        {
            var json = await File.ReadAllTextAsync(_configPath);
            return JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
        }
        catch
        {
            return new AppConfig();
        }
    }

    public async Task SavePathsAsync(string? deviceLibraryPath, string? templatePath, string? outputPath)
    {
        var config = new AppConfig
        {
            DeviceLibraryPath = deviceLibraryPath?.Trim() ?? string.Empty,
            TemplatePath = templatePath?.Trim() ?? string.Empty,
            OutputPath = outputPath?.Trim() ?? string.Empty
        };

        Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);
        var json = JsonSerializer.Serialize(config, JsonOptions);
        await File.WriteAllTextAsync(_configPath, json);
    }
}
