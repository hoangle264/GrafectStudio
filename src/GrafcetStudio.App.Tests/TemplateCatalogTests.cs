using GrafcetStudio.CodeGen.Template;
using Xunit;

namespace GrafcetStudio.App.Tests;

public class TemplateCatalogTests
{
    private static string MakeRoot(params string[] files)
    {
        var root = Path.Combine(Path.GetTempPath(), "tplcat-" + Guid.NewGuid().ToString("N"));
        foreach (var file in files)
        {
            var path = Path.Combine(root, file.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, file.EndsWith("profile.txt") ? "simple" : "x");
        }
        return root;
    }

    [Fact]
    public void List_ShowsOnlyAliasTheLoaderUses_AndNotesTheIgnoredOnes()
    {
        var root = MakeRoot("unit-error.hbs", "unitError.hbs", "error.hbs");
        try
        {
            var entries = TemplateCatalog.List(root);
            var error = Assert.Single(entries);
            Assert.Equal("unitError.hbs", error.Path);
            Assert.Equal(new[] { "unit-error.hbs", "error.hbs" }, error.Ignored);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void List_MarksPackmlInactiveUnlessProfileSelectsIt()
    {
        var root = MakeRoot("profile.txt", "simple/UnitCode.hbs", "packml/UnitCode.hbs");
        try
        {
            var entries = TemplateCatalog.List(root);
            Assert.True(entries.Single(e => e.Path == "simple/UnitCode.hbs").Active);
            Assert.False(entries.Single(e => e.Path == "packml/UnitCode.hbs").Active);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void List_PutsSiemensLadTemplateUnderItsOwnPlatform()
    {
        var root = MakeRoot("default.lad.hbs");
        try
        {
            var entry = Assert.Single(TemplateCatalog.List(root));
            Assert.Equal("Siemens LAD", entry.Platform);
        }
        finally { Directory.Delete(root, true); }
    }
}
