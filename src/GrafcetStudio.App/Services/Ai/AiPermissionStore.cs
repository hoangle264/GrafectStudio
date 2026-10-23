using System.IO;
using System.Text;
using System.Text.Json;

namespace GrafcetStudio.App.Services.Ai;

/// <summary>
/// Remembers which context scopes the user allowed to be sent to the AI, per project id.
/// Kept in app data (not in the project file) so sharing a project never shares its grants.
/// </summary>
public class AiPermissionStore
{
    private static readonly string[] KnownScopes = { "variable", "unit", "diagram", "step", "io", "structure" };
    private readonly string _path;
    private readonly object _sync = new();

    public AiPermissionStore()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GrafcetStudio"))
    {
    }

    public AiPermissionStore(string directory)
    {
        _path = Path.Combine(directory, "ai_permissions.json");
    }

    public IReadOnlyList<string> Get(string projectId)
    {
        if (string.IsNullOrWhiteSpace(projectId)) return Array.Empty<string>();
        lock (_sync)
        {
            return Load().TryGetValue(projectId, out var scopes) ? scopes : Array.Empty<string>();
        }
    }

    public IReadOnlyList<string> Grant(string projectId, IEnumerable<string> scopes)
    {
        if (string.IsNullOrWhiteSpace(projectId)) throw new ArgumentException("Project id is required.", nameof(projectId));
        lock (_sync)
        {
            var all = Load();
            var current = all.TryGetValue(projectId, out var existing) ? existing : new List<string>();
            foreach (var scope in scopes.Select(s => s.Trim()).Where(s => KnownScopes.Contains(s)))
            {
                if (!current.Contains(scope)) current.Add(scope);
            }
            all[projectId] = current;
            Save(all);
            return current;
        }
    }

    public void Revoke(string projectId)
    {
        if (string.IsNullOrWhiteSpace(projectId)) return;
        lock (_sync)
        {
            var all = Load();
            if (all.Remove(projectId)) Save(all);
        }
    }

    private Dictionary<string, List<string>> Load()
    {
        if (!File.Exists(_path)) return new Dictionary<string, List<string>>();
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, List<string>>>(File.ReadAllText(_path, Encoding.UTF8))
                ?? new Dictionary<string, List<string>>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, List<string>>();
        }
    }

    private void Save(Dictionary<string, List<string>> all)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(all, new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
    }
}
