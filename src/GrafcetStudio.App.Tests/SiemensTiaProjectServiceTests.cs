using GrafcetStudio.App.Services.Siemens;
using GrafcetStudio.TiaBridge.Contracts;
using System.Text.Json;
using Xunit;

namespace GrafcetStudio.App.Tests;

public class SiemensTiaProjectServiceTests
{
    [Fact]
    public async Task UnavailableService_ReturnsClearNotConfiguredFailure()
    {
        var service = new UnavailableSiemensTiaProjectService();
        var request = new SiemensPushRequest
        {
            ProjectPath = @"C:\TiaProjects\Demo.ap17",
            DeviceName = "PLC Station",
            PlcName = "PLC_1",
            TargetFolderPath = "Program blocks/Grafcet",
            BlockName = "Main_Grafcet_LAD",
            XmlContent = "<Document />",
            OverwriteMode = SiemensOverwriteMode.Overwrite
        };

        var result = await service.PushBlockXmlAsync(request);

        Assert.False(result.Ok);
        Assert.Equal(SiemensTiaProjectServiceStatus.NotConfigured, result.Status);
        Assert.Contains("TIA push/import is not configured", result.Message);
        Assert.Contains("GRAFCETSTUDIO_TIA_IMPORT_MODE", result.Message);
        Assert.Equal(request.ProjectPath, result.ProjectPath);
        Assert.Equal(request.DeviceName, result.DeviceName);
        Assert.Equal(request.PlcName, result.PlcName);
        Assert.Equal(request.TargetFolderPath, result.TargetFolderPath);
        Assert.Equal(request.BlockName, result.BlockName);
    }

    [Fact]
    public async Task UnavailableService_HonorsCancellationBeforeReturningFailure()
    {
        var service = new UnavailableSiemensTiaProjectService();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.PushBlockXmlAsync(new SiemensPushRequest { BlockName = "Main" }, cancellation.Token));
    }

    [Fact]
    public async Task ReflectionService_ReturnsUnavailableWhenOpennessAssemblyIsMissing()
    {
        var service = new ReflectionSiemensTiaProjectService(() => Path.Combine(Path.GetTempPath(), "missing-tia-openness-" + Guid.NewGuid().ToString("N")));
        var request = new SiemensPushRequest
        {
            DeviceName = "PLC Station",
            PlcName = "PLC_1",
            TargetFolderPath = "Program blocks/Grafcet",
            BlockName = "Main_Grafcet_LAD",
            XmlContent = "<Document />"
        };

        var result = await service.PushBlockXmlAsync(request);

        Assert.False(result.Ok);
        Assert.Equal(SiemensTiaProjectServiceStatus.TiaOpennessUnavailable, result.Status);
        Assert.Contains("TIA Openness assembly", result.Message);
        Assert.Contains(ReflectionSiemensTiaProjectService.OpennessDirectoryEnvironmentVariable, result.Message);
    }

    [Fact]
    public async Task ReflectionService_ValidatesRequiredImportRequestFields()
    {
        var service = new ReflectionSiemensTiaProjectService(() => null);

        var result = await service.PushBlockXmlAsync(new SiemensPushRequest { XmlContent = "<Document />" });

        Assert.False(result.Ok);
        Assert.Equal(SiemensTiaProjectServiceStatus.InvalidRequest, result.Status);
        Assert.Contains("DeviceName is required", result.Message);
    }

    [Fact]
    public void BridgeRequest_SerializesExpectedFields()
    {
        var request = new TiaBridgeRequest
        {
            TiaVersion = "V19",
            ProjectPath = @"C:\TIA\Demo.ap19",
            DeviceName = "PLC Station",
            PlcName = "PLC_1",
            TargetFolderPath = "Program blocks/Grafcet",
            BlockName = "Main_Grafcet_LAD",
            XmlPath = @"C:\Temp\Main_Grafcet_LAD.xml",
            OverwriteMode = TiaBridgeOverwriteMode.Overwrite
        };

        var json = JsonSerializer.Serialize(request);

        Assert.Contains("\"TiaVersion\":\"V19\"", json);
        Assert.Contains("\"ProjectPath\":\"C:\\\\TIA\\\\Demo.ap19\"", json);
        Assert.Contains("\"OverwriteMode\":1", json);
    }

    [Fact]
    public async Task BridgeService_ReturnsBridgeNotFoundWhenPathIsMissing()
    {
        var service = new BridgeSiemensTiaProjectService(() => Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "GrafcetStudio.TiaBridge.V19.exe"));
        var request = new SiemensPushRequest
        {
            DeviceName = "PLC Station",
            PlcName = "PLC_1",
            TargetFolderPath = "Program blocks/Grafcet",
            BlockName = "Main_Grafcet_LAD",
            XmlContent = "<Document />"
        };

        var result = await service.PushBlockXmlAsync(request);

        Assert.False(result.Ok);
        Assert.Equal(SiemensTiaProjectServiceStatus.BridgeNotFound, result.Status);
        Assert.Contains("generated successfully", result.Message);
    }

    [Fact]
    public async Task BridgeService_TimesOutWhenBridgeHangs()
    {
        var bridgePath = await CreateCmdBridgeAsync(new[] { "@echo off", "ping 127.0.0.1 -n 6 >nul" });

        try
        {
            var service = new BridgeSiemensTiaProjectService(() => bridgePath, TimeSpan.FromMilliseconds(150));
            var request = new SiemensPushRequest
            {
                DeviceName = "PLC Station",
                PlcName = "PLC_1",
                TargetFolderPath = "Program blocks/Grafcet",
                BlockName = "Main_Grafcet_LAD",
                XmlContent = "<Document />"
            };

            var result = await service.PushBlockXmlAsync(request);

            Assert.False(result.Ok);
            Assert.Equal(SiemensTiaProjectServiceStatus.Timeout, result.Status);
            Assert.Contains("timed out", result.Message);
        }
        finally
        {
            File.Delete(bridgePath);
        }
    }

    [Fact]
    public async Task BridgeService_ReturnsFailureForInvalidStdout()
    {
        var bridgePath = await CreateCmdBridgeAsync(new[] { "@echo off", "echo not-json" });

        try
        {
            var service = new BridgeSiemensTiaProjectService(() => bridgePath, TimeSpan.FromSeconds(5));
            var request = new SiemensPushRequest
            {
                DeviceName = "PLC Station",
                PlcName = "PLC_1",
                TargetFolderPath = "Program blocks/Grafcet",
                BlockName = "Main_Grafcet_LAD",
                XmlContent = "<Document />"
            };

            var result = await service.PushBlockXmlAsync(request);

            Assert.False(result.Ok);
            Assert.Equal(SiemensTiaProjectServiceStatus.ImportFailed, result.Status);
            Assert.Contains("invalid JSON stdout", result.Message);
        }
        finally
        {
            File.Delete(bridgePath);
        }
    }

    [Fact]
    public async Task BridgeService_ValidatesMissingDeviceNameBeforeStartingBridge()
    {
        var service = new BridgeSiemensTiaProjectService(() => "ignored.exe");

        var result = await service.PushBlockXmlAsync(new SiemensPushRequest
        {
            PlcName = "PLC_1",
            TargetFolderPath = "Program blocks/Grafcet",
            BlockName = "Main_Grafcet_LAD",
            XmlContent = "<Document />"
        });

        Assert.False(result.Ok);
        Assert.Equal(SiemensTiaProjectServiceStatus.InvalidRequest, result.Status);
        Assert.Contains("DeviceName is required", result.Message);
    }

    private static async Task<string> CreateCmdBridgeAsync(string[] lines)
    {
        var bridgePath = Path.Combine(Path.GetTempPath(), $"fake-bridge-{Guid.NewGuid():N}.cmd");
        await File.WriteAllLinesAsync(bridgePath, lines.Concat(new[] { "exit /b 0" }));
        return bridgePath;
    }
}


