using GrafcetStudio.App.Services.Ai;
using Xunit;

namespace GrafcetStudio.App.Tests;

public class AiKeyStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "grafcet-aikey-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    [Fact]
    public void Save_EncryptsKeyOnDisk_AndRoundTrips()
    {
        var store = new AiKeyStore(_dir);
        store.Save("secret-key-123", "gemini-test");

        var raw = File.ReadAllBytes(Path.Combine(_dir, "ai_key.bin"));
        Assert.DoesNotContain("secret-key-123", System.Text.Encoding.UTF8.GetString(raw));
        Assert.Equal("secret-key-123", store.LoadStoredKey());
        Assert.Equal("gemini-test", store.LoadStoredModel());
    }

    [Fact]
    public void Clear_RemovesStoredKey()
    {
        var store = new AiKeyStore(_dir);
        store.Save("k", null);
        store.Clear();
        Assert.Null(store.LoadStoredKey());
    }

    [Fact]
    public void Save_RejectsEmptyKey()
    {
        Assert.Throws<ArgumentException>(() => new AiKeyStore(_dir).Save("  ", null));
    }
}
