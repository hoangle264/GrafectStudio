using GrafcetStudio.CodeGen.Runtime;
using GrafcetStudio.CodeGen.Runtime.Models;
using GrafcetStudio.Domain.Models;
using GrafcetStudio.Domain.Resolution;
using Xunit;

namespace GrafcetStudio.App.Tests;

public class SignalLabelFallbackTests
{
    private static readonly List<DeviceVariable> Variables = new()
    {
        new DeviceVariable
        {
            Label = "Cyl1",
            Format = "cylinder",
            SignalAddresses = new Dictionary<string, string> { ["Fwd"] = "R500", ["FwdSensor"] = "" }
        },
        new DeviceVariable { Label = "StartBtn", Format = "bool" }
    };

    [Theory]
    [InlineData("Cyl1.Fwd", "R500")]
    [InlineData("Cyl1.FwdSensor", "Cyl1.FwdSensor")]
    [InlineData("cyl1.Bwd", "Cyl1.Bwd")]
    [InlineData("StartBtn", "StartBtn")]
    [InlineData("DM100", "DM100")]
    public void ResolveAddress_UsesLabelWhenAddressMissing(string reference, string expected)
    {
        Assert.Equal(expected, SignalResolver.ResolveAddress(reference, Variables, PlcAddressFormats.Keyence));
    }

    [Fact]
    public void DeviceCommand_ResolvesWithLabelWhenSignalAddressMissing()
    {
        var library = new DeviceLibraryRoot(new List<DeviceLibraryEntry>
        {
            new()
            {
                DeviceId = "cylinder",
                Commands = new Dictionary<string, DeviceCommand>
                {
                    ["Bwd"] = new() { DriveSignal = "Bwd", Complete = new DeviceCommandComplete { Sensor = "FwdSensor" } }
                }
            }
        });
        var action = new StepAction { Variable = "Cyl1.Bwd" };

        var result = DeviceCommandResolver.Resolve(action, "@MR300", Variables, library);

        Assert.NotNull(result);
        Assert.Equal("Cyl1.Bwd", result!.Execute.PhysicalAddress);
        Assert.Equal("Cyl1.FwdSensor", result.FeedbackSignals.Single().PhysicalAddress);
        Assert.Equal(2, result.Diagnostics.Count(d => d.Code == "DEVICE_SIGNAL_LABEL_FALLBACK"));
    }

    [Theory]
    [InlineData("R1000", true)]
    [InlineData("@MR300", true)]
    [InlineData("DM100.5", true)]
    [InlineData("CR2002", true)]
    [InlineData("Cyl1", false)]
    [InlineData("MR300Extra", false)]
    [InlineData("%I0.0", false)]
    public void KeyenceFormat_MatchesOnlyKeyenceDevices(string value, bool expected)
    {
        Assert.Equal(expected, PlcAddressFormats.Keyence.IsAddress(value));
    }

    [Theory]
    [InlineData("%I0.0", true)]
    [InlineData("Q1.7", true)]
    [InlineData("%MW10", true)]
    [InlineData("%DB1.DBX0.0", true)]
    [InlineData("\"Motor\".Status.%X3", true)]
    [InlineData("R1000", false)]
    [InlineData("%I0.8", false)]
    public void SiemensFormat_MatchesOnlySiemensAddresses(string value, bool expected)
    {
        Assert.Equal(expected, PlcAddressFormats.Siemens.IsAddress(value));
    }

    [Fact]
    public void Payload_FillsMissingAddressesWithLabels()
    {
        var payload = new CodegenPayload
        {
            DeviceTypes = new List<DeviceType>
            {
                new() { Name = "cylinder", Signals = new List<DeviceSignal> { new() { Id = "s1", Name = "Fwd" }, new() { Id = "s2", Name = "Bwd" } } }
            },
            Variables = new List<DeviceVariable>
            {
                new() { Label = "Cyl1", Format = "cylinder", SignalAddresses = new Dictionary<string, string> { ["Fwd"] = "R500" } },
                new() { Label = "StartBtn", Format = "bool" },
                new() { Label = "StopBtn", Format = "bool", Address = "R001" }
            },
            UnitConfig = new Dictionary<string, GrafcetStudio.Domain.Models.UnitConfig>
            {
                ["u1"] = new() { Label = "Unit1", SignalAddresses = new Dictionary<string, string> { ["Auto"] = "" } }
            },
            System = new SystemControlInfo { Label = "Sys", SignalAddresses = new Dictionary<string, string> { ["Emg"] = "" } }
        };

        payload.EnrichVariables();

        Assert.Equal("R500", payload.Variables[0].SignalAddresses["Fwd"]);
        Assert.Equal("Cyl1.Bwd", payload.Variables[0].SignalAddresses["Bwd"]);
        Assert.Null(payload.Variables[0].Address);
        Assert.Equal("StartBtn", payload.Variables[1].Address);
        Assert.Equal("R001", payload.Variables[2].Address);
        Assert.Equal("Unit1.Auto", payload.UnitConfig["u1"].SignalAddresses["Auto"]);
        Assert.Equal("Sys.Emg", payload.System!.SignalAddresses["Emg"]);
    }
}
