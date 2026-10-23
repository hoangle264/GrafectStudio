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

public class TemplateSyntaxCheckerTests
{
    [Theory]
    [InlineData("{{#each a}}{{name}}{{/each}}")]
    [InlineData("{{#unless a}}x{{else}}y{{/unless}}")]
    public void Check_ReturnsNullForValidTemplates(string source) => Assert.Null(TemplateSyntaxChecker.Check(source));

    [Theory]
    [InlineData("{{#unless a}}x")]
    [InlineData("{{#with a}}{{#each b}}{{/with}}{{/each}}")]
    [InlineData("{{#if a}}x{{/each}}")]
    [InlineData("{{foo")]
    public void Check_ReturnsMessageForBrokenTemplates(string source) => Assert.False(string.IsNullOrEmpty(TemplateSyntaxChecker.Check(source)));
}

public class TemplatePreviewServiceTests
{
    private static void WriteBaseTemplates(string root)
    {
        var simple = Path.Combine(root, "simple");
        Directory.CreateDirectory(simple);
        foreach (var name in new[] { "Error", "System", "SystemControl", "DeviceManager", "MapIO" })
        {
            File.WriteAllText(Path.Combine(simple, name + ".hbs"), "; " + name);
        }
    }

    [Fact]
    public void Render_UsesUnsavedTextForTheEditedFileOnly()
    {
        var root = Path.Combine(Path.GetTempPath(), "tplprev-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var file = Path.Combine(root, "unitMainOutput.hbs");
        File.WriteAllText(file, "ON-DISK");
        WriteBaseTemplates(root);
        try
        {
            var payload = new GrafcetStudio.Domain.Models.CodegenPayload
            {
                Project = new GrafcetStudio.Domain.Models.ProjectInfo { Name = "Demo" },
                Unit = new GrafcetStudio.Domain.Models.UnitInfo { Name = "Main" }
            };

            var result = TemplatePreviewService.Render(root, Path.GetFullPath(file), "UNSAVED {{unit.name}}", payload);

            var unit = Assert.Single(result.Files, f => f.Path.StartsWith("Units/"));
            Assert.Contains("UNSAVED", unit.Content);
            Assert.DoesNotContain("ON-DISK", unit.Content);
            Assert.Empty(result.Warnings);
            Assert.Equal("ON-DISK", File.ReadAllText(file));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Render_WarnsWhenEditedFileIsNotLoadedByTheGenerator()
    {
        var root = Path.Combine(Path.GetTempPath(), "tplprev-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "unitError.hbs"), "A");
        File.WriteAllText(Path.Combine(root, "unit-error.hbs"), "B");
        WriteBaseTemplates(root);
        try
        {
            var payload = new GrafcetStudio.Domain.Models.CodegenPayload
            {
                Unit = new GrafcetStudio.Domain.Models.UnitInfo { Name = "Main" }
            };

            var result = TemplatePreviewService.Render(root, Path.GetFullPath(Path.Combine(root, "unit-error.hbs")), "X", payload);

            Assert.Single(result.Warnings);
        }
        finally { Directory.Delete(root, true); }
    }
}

public class TemplateVariableCatalogTests
{
    private static GrafcetStudio.Domain.Models.CodegenPayload Payload() => new()
    {
        Project = new GrafcetStudio.Domain.Models.ProjectInfo { Name = "Demo" },
        Unit = new GrafcetStudio.Domain.Models.UnitInfo { Name = "Main" },
        Variables = new List<GrafcetStudio.Domain.Models.DeviceVariable>
        {
            new() { Label = "UnitMain", Format = "Motor", Address = "@M1" }
        },
        DeviceTypes = new List<GrafcetStudio.Domain.Models.DeviceType>
        {
            new() { Name = "Motor", Signals = new List<GrafcetStudio.Domain.Models.DeviceSignal> { new() { Id = "run", Name = "Run" } } }
        }
    };

    [Fact]
    public void Build_UnitTemplate_ExposesGeneratorContext()
    {
        var root = TemplateVariableCatalog.Build("unitAuto.hbs", Payload());
        Assert.Contains(root.Children, c => c.Name == "devices" && c.Kind == "list");
        var devices = root.Children.Single(c => c.Name == "devices");
        Assert.Contains(devices.Children, c => c.Name == "label" && c.Sample == "UnitMain");
        Assert.Contains(devices.Children.Single(c => c.Name == "signals").Children, c => c.Name == "name");
    }

    [Fact]
    public void Build_DevicePartial_StartsAtTheDeviceElement()
    {
        var root = TemplateVariableCatalog.Build("devices/cylinder.hbs", Payload());
        Assert.Contains(root.Children, c => c.Name == "partialName");
    }

    [Fact]
    public void Build_ErrorTemplate_UsesTheErrorGeneratorModel()
    {
        var root = TemplateVariableCatalog.Build("simple/Error.hbs", Payload());
        Assert.DoesNotContain(root.Children, c => c.Name == "devices");
    }
}
