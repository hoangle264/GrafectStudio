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
    public SiemensTiaConfig SiemensTia { get; set; } = new();
}

public class SiemensTiaConfig
{
    public string ProjectPath { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string PlcName { get; set; } = string.Empty;
    public string TargetFolderPath { get; set; } = "Program blocks";
    public string OverwriteMode { get; set; } = "FailIfExists";
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

    public ConfigService(string configPath)
    {
        _configPath = configPath;
    }

    public async Task<AppConfig> LoadAsync()
    {
        if (!File.Exists(_configPath)) return new AppConfig();

        try
        {
            var json = await File.ReadAllTextAsync(_configPath);
            var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
            config.SiemensTia ??= new SiemensTiaConfig();
            return config;
        }
        catch
        {
            return new AppConfig();
        }
    }

    public async Task SavePathsAsync(string? deviceLibraryPath, string? templatePath, string? outputPath)
    {
        var config = await LoadAsync();
        config.DeviceLibraryPath = deviceLibraryPath?.Trim() ?? string.Empty;
        config.TemplatePath = templatePath?.Trim() ?? string.Empty;
        config.OutputPath = outputPath?.Trim() ?? string.Empty;

        await SaveAsync(config);
    }

    public async Task SaveSiemensTiaConfigAsync(string? projectPath, string? deviceName, string? plcName, string? targetFolderPath, string? overwriteMode)
    {
        var config = await LoadAsync();
        config.SiemensTia = new SiemensTiaConfig
        {
            ProjectPath = projectPath?.Trim() ?? string.Empty,
            DeviceName = deviceName?.Trim() ?? string.Empty,
            PlcName = plcName?.Trim() ?? string.Empty,
            TargetFolderPath = string.IsNullOrWhiteSpace(targetFolderPath) ? "Program blocks" : targetFolderPath.Trim(),
            OverwriteMode = string.IsNullOrWhiteSpace(overwriteMode) ? "FailIfExists" : overwriteMode.Trim()
        };

        await SaveAsync(config);
    }

    private async Task SaveAsync(AppConfig config)
    {
        config.SiemensTia ??= new SiemensTiaConfig();

        Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);
        var json = JsonSerializer.Serialize(config, JsonOptions);
        await File.WriteAllTextAsync(_configPath, json);
    }
}
