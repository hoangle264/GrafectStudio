using GrafcetStudio.App.Services.Ai;
using Xunit;

namespace GrafcetStudio.App.Tests;

public class AiPermissionStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "grafcet-aiperm-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    [Fact]
    public void Grant_PersistsPerProject_AndIgnoresUnknownScopes()
    {
        new AiPermissionStore(_dir).Grant("proj-a", new[] { "variable", "io", "secrets" });

        var store = new AiPermissionStore(_dir);
        Assert.Equal(new[] { "variable", "io" }, store.Get("proj-a"));
        Assert.Empty(store.Get("proj-b"));
    }

    [Fact]
    public void Grant_MergesWithoutDuplicates()
    {
        var store = new AiPermissionStore(_dir);
        store.Grant("p", new[] { "variable" });
        Assert.Equal(new[] { "variable", "step" }, store.Grant("p", new[] { "variable", "step" }));
    }

    [Fact]
    public void Revoke_RemovesProjectGrants()
    {
        var store = new AiPermissionStore(_dir);
        store.Grant("p", new[] { "variable" });
        store.Revoke("p");
        Assert.Empty(store.Get("p"));
    }

    [Fact]
    public void AuditLog_RecordsShapeButNotContent()
    {
        var path = Path.Combine(_dir, "ai_audit.log");
        var request = new SanitizedAiRequest("req-1", "map-io", "secret message", "{\"context\":{\"variables\":[{\"label\":\"SecretTag\"}]}}");

        AiAuditLog.Record(request, "gemini", "gemini-test", path);

        var text = File.ReadAllText(path);
        Assert.Contains("req-1", text);
        Assert.Contains("variables", text);
        Assert.DoesNotContain("SecretTag", text);
        Assert.DoesNotContain("secret message", text);
    }
}
