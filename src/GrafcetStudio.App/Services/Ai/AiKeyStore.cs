using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GrafcetStudio.App.Services.Ai;

public sealed record AiSettingsSnapshot(bool Configured, string Source, string Model);

/// <summary>
/// Stores the Gemini API key encrypted with Windows DPAPI (current user) and the model name in plain JSON.
/// The key never leaves the host: the WebView only ever receives <see cref="AiSettingsSnapshot"/>.
/// </summary>
public class AiKeyStore
{
    public const string DefaultModel = "gemini-2.0-flash";

    private readonly string _keyPath;
    private readonly string _settingsPath;

    public AiKeyStore()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GrafcetStudio"))
    {
    }

    public AiKeyStore(string directory)
    {
        _keyPath = Path.Combine(directory, "ai_key.bin");
        _settingsPath = Path.Combine(directory, "ai_settings.json");
    }

    public void Save(string apiKey, string? model)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) throw new ArgumentException("API key is empty.", nameof(apiKey));

        Directory.CreateDirectory(Path.GetDirectoryName(_keyPath)!);
        var encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(apiKey.Trim()), null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_keyPath, encrypted);
        SaveModel(model);
    }

    public void SaveModel(string? model)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
        File.WriteAllText(_settingsPath, JsonSerializer.Serialize(new { model = model?.Trim() ?? string.Empty }), Encoding.UTF8);
    }

    public void Clear()
    {
        if (File.Exists(_keyPath)) File.Delete(_keyPath);
    }

    /// <summary>Returns the key saved from the UI, or null when none is stored or it cannot be decrypted.</summary>
    public string? LoadStoredKey()
    {
        if (!File.Exists(_keyPath)) return null;
        try
        {
            var raw = ProtectedData.Unprotect(File.ReadAllBytes(_keyPath), null, DataProtectionScope.CurrentUser);
            var key = Encoding.UTF8.GetString(raw).Trim();
            return key.Length == 0 ? null : key;
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    public string? LoadStoredModel()
    {
        if (!File.Exists(_settingsPath)) return null;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(_settingsPath, Encoding.UTF8));
            return doc.RootElement.TryGetProperty("model", out var model) && model.ValueKind == JsonValueKind.String
                ? model.GetString()?.Trim()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Resolves the effective key: UI-saved key first, then the environment variables.</summary>
    public (string? Key, string Source) ResolveKey()
    {
        var stored = LoadStoredKey();
        if (!string.IsNullOrWhiteSpace(stored)) return (stored, "ui");

        var env = Environment.GetEnvironmentVariable("GEMINI_API_KEY")?.Trim();
        if (string.IsNullOrWhiteSpace(env)) env = Environment.GetEnvironmentVariable("GRAFCETSTUDIO_GEMINI_API_KEY")?.Trim();
        return string.IsNullOrWhiteSpace(env) ? (null, "none") : (env, "env");
    }

    public string ResolveModel()
    {
        var stored = LoadStoredModel();
        if (!string.IsNullOrWhiteSpace(stored)) return stored;
        var env = Environment.GetEnvironmentVariable("GRAFCETSTUDIO_GEMINI_MODEL")?.Trim();
        return string.IsNullOrWhiteSpace(env) ? DefaultModel : env;
    }

    public AiSettingsSnapshot GetSnapshot()
    {
        var (key, source) = ResolveKey();
        return new AiSettingsSnapshot(!string.IsNullOrWhiteSpace(key), source, ResolveModel());
    }
}
