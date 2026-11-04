using GrafcetStudio.App.Generators.Keyence;
using GrafcetStudio.Domain.Models;
using GrafcetStudio.Domain.Resolution;
using Xunit;

namespace GrafcetStudio.App.Tests;

public class NestedStructTests
{
    // Station (level 1) -> Cyl (level 2) -> Sensor (level 3)
    private static List<DeviceType> Types() => new()
    {
        new() { Id = "t-station", Name = "Station", Signals = new List<DeviceSignal>
        {
            new() { Id = "Cyl", Name = "Cyl", DataType = "Cyl", NestedTypeId = "t-cyl" },
            new() { Id = "Run", Name = "Run", DataType = "Bool" }
        } },
        new() { Id = "t-cyl", Name = "Cyl", Signals = new List<DeviceSignal>
        {
            new() { Id = "Home", Name = "Home", DataType = "Sensor" },
            new() { Id = "Work", Name = "Work", DataType = "renamed-later", NestedTypeId = "t-sensor" },
            new() { Id = "Coil", Name = "Coil", DataType = "Bool", Comment = "Valve coil" }
        } },
        new() { Id = "t-sensor", Name = "Sensor", Signals = new List<DeviceSignal>
        {
            new() { Id = "On", Name = "On", DataType = "Bool" },
            new() { Id = "Delay", Name = "Delay", DataType = "Time" }
        } }
    };

    [Fact]
    public void FlattenLeaves_UsesDottedPathsThroughEveryLevel()
    {
        var types = Types();

        var paths = StructNesting.FlattenLeaves(types[0], types).Select(leaf => leaf.Path);

        Assert.Equal(new[] { "Cyl.Home.On", "Cyl.Home.Delay", "Cyl.Work.On", "Cyl.Work.Delay", "Cyl.Coil", "Run" }, paths);
        Assert.Equal(new[] { "Cyl", "Cyl.Home", "Cyl.Work" }, StructNesting.ContainerPaths(types[0], types));
    }

    [Fact]
    public void FlattenLeaves_StopsAtDepthLimitAndLoops()
    {
        var types = Types();
        types.Add(new DeviceType { Id = "t-top", Name = "Top", Signals = new List<DeviceSignal> { new() { Id = "St", Name = "St", NestedTypeId = "t-station" } } });
        types.Add(new DeviceType { Id = "t-a", Name = "A", Signals = new List<DeviceSignal> { new() { Id = "B", Name = "B", DataType = "B" } } });
        types.Add(new DeviceType { Id = "t-b", Name = "B", Signals = new List<DeviceSignal> { new() { Id = "A", Name = "A", DataType = "A" } } });

        var tooDeep = StructNesting.FlattenLeaves(types.Single(t => t.Name == "Top"), types).Select(leaf => leaf.Path).ToList();
        var loop = StructNesting.FlattenLeaves(types.Single(t => t.Name == "A"), types).Select(leaf => leaf.Path).ToList();

        Assert.Contains("St.Cyl.Home", tooDeep); // level 4 is not expanded
        Assert.DoesNotContain("St.Cyl.Home.On", tooDeep);
        Assert.Equal(new[] { "B.A" }, loop);
    }

    [Fact]
    public void Payload_FillsNestedLeavesWithLabelsAndDropsContainerKeys()
    {
        var payload = new CodegenPayload
        {
            DeviceTypes = Types(),
            Variables = new List<DeviceVariable>
            {
                new()
                {
                    Label = "Station1",
                    Format = "Station",
                    SignalAddresses = new Dictionary<string, string> { ["Cyl.Home.On"] = "R500", ["Cyl"] = "" }
                }
            }
        };

        payload.EnrichVariables();
        var addresses = payload.Variables[0].SignalAddresses;

        Assert.Equal("R500", addresses["Cyl.Home.On"]);
        Assert.Equal("Station1.Cyl.Work.Delay", addresses["Cyl.Work.Delay"]);
        Assert.Equal("Station1.Run", addresses["Run"]);
        Assert.False(addresses.ContainsKey("Cyl"));
    }

    [Theory]
    [InlineData("Station1.Cyl.Home.On", "R500")]
    [InlineData("Station1.Cyl.Work.On", "Station1.Cyl.Work.On")]
    public void ResolveAddress_HandlesNestedPaths(string reference, string expected)
    {
        var vars = new List<DeviceVariable>
        {
            new() { Label = "Station1", Format = "Station", SignalAddresses = new Dictionary<string, string> { ["Cyl.Home.On"] = "R500" } }
        };

        Assert.Equal(expected, SignalResolver.ResolveAddress(reference, vars, PlcAddressFormats.Keyence));
    }

    [Fact]
    public void Declarations_ListTypesBeforeTheirUsersThenGlobals()
    {
        var payload = new CodegenPayload
        {
            DeviceTypes = Types(),
            Variables = new List<DeviceVariable>
            {
                new() { Label = "Station1", Format = "Station" },
                new() { Label = "StartBtn", Format = "Bool", Address = "R001" },
                new() { Label = "Count", Format = "DInt" }
            }
        };

        var text = KeyenceDeclarationGenerator.Generate(payload);

        Assert.True(text.IndexOf("TYPE Sensor :", StringComparison.Ordinal) < text.IndexOf("TYPE Cyl :", StringComparison.Ordinal));
        Assert.True(text.IndexOf("TYPE Cyl :", StringComparison.Ordinal) < text.IndexOf("TYPE Station :", StringComparison.Ordinal));
        Assert.Contains("    Home : Sensor;", text);
        Assert.Contains("    Work : Sensor;", text);
        Assert.Contains("    Coil : BOOL; (* Valve coil *)", text);
        Assert.Contains("    Delay : TIME;", text);
        Assert.Contains("    Station1 : Station;", text);
        Assert.Contains("    StartBtn : BOOL; (* device R001 *)", text);
        Assert.Contains("    Count : DINT;", text);
        Assert.Equal(1, CountOf(text, "TYPE Sensor :"));
        Assert.DoesNotContain("WARNING", text);
    }

    [Fact]
    public void Declarations_WarnAboutLabelsThatLookLikeDevices()
    {
        var payload = new CodegenPayload
        {
            Variables = new List<DeviceVariable> { new() { Label = "MR100", Format = "Bool" }, new() { Label = "Ok", Format = "Weird" } }
        };

        var text = KeyenceDeclarationGenerator.Generate(payload);

        Assert.Contains("WARNING: Variable \"MR100\" looks like a KV device", text);
        Assert.Contains("WARNING: Unknown data type \"Weird\"", text);
        Assert.DoesNotContain("    MR100 :", text);
        Assert.Contains("    Ok : Weird;", text);
    }

    [Fact]
    public void Declarations_EmptyWhenNoVariables()
    {
        Assert.Equal(string.Empty, KeyenceDeclarationGenerator.Generate(new CodegenPayload()));
    }

    private static int CountOf(string text, string value)
    {
        var count = 0;
        for (var i = text.IndexOf(value, StringComparison.Ordinal); i >= 0; i = text.IndexOf(value, i + 1, StringComparison.Ordinal)) count++;
        return count;
    }
}
