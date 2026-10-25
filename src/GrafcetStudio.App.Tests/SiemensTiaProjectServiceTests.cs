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
            TiaVersion = "V20",
            ProjectPath = @"C:\TIA\Demo.ap20",
            DeviceName = "PLC Station",
            PlcName = "PLC_1",
            TargetFolderPath = "Program blocks/Grafcet",
            BlockName = "Main_Grafcet_LAD",
            XmlPath = @"C:\Temp\Main_Grafcet_LAD.xml",
            OverwriteMode = TiaBridgeOverwriteMode.Overwrite
        };

        var json = JsonSerializer.Serialize(request);

        Assert.Contains("\"TiaVersion\":\"V20\"", json);
        Assert.Contains("\"ProjectPath\":\"C:\\\\TIA\\\\Demo.ap20\"", json);
        Assert.Contains("\"OverwriteMode\":1", json);
    }

    [Theory]
    [InlineData("V19", "V19")]
    [InlineData("v20", "V20")]
    [InlineData("20", "V20")]
    [InlineData(" V20 ", "V20")]
    [InlineData("V18", null)]
    [InlineData("", null)]
    public void TiaVersions_NormalizeAcceptsOnlySupportedVersions(string input, string? expected)
    {
        Assert.Equal(expected, SiemensTiaVersions.Normalize(input));
    }

    [Theory]
    [InlineData("V19")]
    [InlineData("V20")]
    public async Task BridgeService_SendsRequestedTiaVersionToMatchingBridge(string tiaVersion)
    {
        var capturePath = Path.Combine(Path.GetTempPath(), $"grafcetstudio-captured-request-{Guid.NewGuid():N}.json");
        var bridgePath = await CreateCmdBridgeAsync(new[]
        {
            "@echo off",
            $"copy /y %2 \"{capturePath}\" >nul",
            "echo {\"Success\":true,\"Status\":\"Success\",\"Message\":\"ok\"}"
        });
        string? requestedVersion = null;

        try
        {
            var service = new BridgeSiemensTiaProjectService(version => { requestedVersion = version; return bridgePath; }, TimeSpan.FromSeconds(10));

            var result = await service.PushBlockXmlAsync(new SiemensPushRequest
            {
                DeviceName = "PLC Station",
                PlcName = "PLC_1",
                TargetFolderPath = "Program blocks",
                BlockName = "Main_Grafcet_LAD",
                XmlContent = "<Document />",
                TiaVersion = tiaVersion
            });

            Assert.True(result.Ok, result.Message);
            Assert.Equal(tiaVersion, requestedVersion);
            Assert.Contains($"\"TiaVersion\":\"{tiaVersion}\"", await File.ReadAllTextAsync(capturePath));
        }
        finally
        {
            File.Delete(bridgePath);
            File.Delete(capturePath);
        }
    }

    [Fact]
    public async Task BridgeService_RejectsUnsupportedTiaVersionBeforeStartingBridge()
    {
        var service = new BridgeSiemensTiaProjectService(() => "ignored.exe");

        var result = await service.PushBlockXmlAsync(new SiemensPushRequest
        {
            DeviceName = "PLC Station",
            PlcName = "PLC_1",
            TargetFolderPath = "Program blocks",
            BlockName = "Main_Grafcet_LAD",
            XmlContent = "<Document />",
            TiaVersion = "V18"
        });

        Assert.False(result.Ok);
        Assert.Equal(SiemensTiaProjectServiceStatus.InvalidRequest, result.Status);
        Assert.Contains("V18", result.Message);
    }

    [Fact]
    public async Task BridgeService_NotFoundMessageNamesVersionedExecutable()
    {
        var service = new BridgeSiemensTiaProjectService(_ => null);

        var result = await service.PushBlockXmlAsync(new SiemensPushRequest
        {
            DeviceName = "PLC Station",
            PlcName = "PLC_1",
            TargetFolderPath = "Program blocks",
            BlockName = "Main_Grafcet_LAD",
            XmlContent = "<Document />",
            TiaVersion = "V20"
        });

        Assert.Equal(SiemensTiaProjectServiceStatus.BridgeNotFound, result.Status);
        Assert.Contains("GrafcetStudio.TiaBridge.V20.exe", result.Message);
        Assert.Contains("TIA Portal V20", result.Message);
    }

    [Fact]
    public void ResolveDefaultBridgePath_PicksExecutableForRequestedVersion()
    {
        var baseDirectory = CreateTempDirectory();
        try
        {
            var shippedDirectory = Directory.CreateDirectory(Path.Combine(baseDirectory, "tia-bridge")).FullName;
            File.WriteAllText(Path.Combine(shippedDirectory, "GrafcetStudio.TiaBridge.V19.exe"), string.Empty);
            File.WriteAllText(Path.Combine(shippedDirectory, "GrafcetStudio.TiaBridge.V20.exe"), string.Empty);
            File.WriteAllText(Path.Combine(baseDirectory, "GrafcetStudio.TiaBridge.V20.exe"), string.Empty);

            Assert.Equal(Path.Combine(shippedDirectory, "GrafcetStudio.TiaBridge.V19.exe"),
                BridgeSiemensTiaProjectService.ResolveDefaultBridgePath("V19", baseDirectory, _ => null));
            // Next to the app wins over tia-bridge/.
            Assert.Equal(Path.Combine(baseDirectory, "GrafcetStudio.TiaBridge.V20.exe"),
                BridgeSiemensTiaProjectService.ResolveDefaultBridgePath("V20", baseDirectory, _ => null));
        }
        finally
        {
            Directory.Delete(baseDirectory, true);
        }
    }

    [Fact]
    public void ResolveDefaultBridgePath_VersionedEnvironmentVariableWinsAndFolderValuesGetExecutableName()
    {
        var bridgeFolder = CreateTempDirectory();
        try
        {
            var environment = new Dictionary<string, string?>
            {
                ["GRAFCETSTUDIO_TIA_BRIDGE_PATH_V20"] = @"C:\Bridges\Custom.V20.exe",
                ["GRAFCETSTUDIO_TIA_BRIDGE_PATH"] = bridgeFolder
            };
            string? Lookup(string name) => environment.TryGetValue(name, out var value) ? value : null;

            Assert.Equal(@"C:\Bridges\Custom.V20.exe",
                BridgeSiemensTiaProjectService.ResolveDefaultBridgePath("V20", bridgeFolder, Lookup));
            Assert.Equal(Path.Combine(bridgeFolder, "GrafcetStudio.TiaBridge.V19.exe"),
                BridgeSiemensTiaProjectService.ResolveDefaultBridgePath("V19", bridgeFolder, Lookup));
        }
        finally
        {
            Directory.Delete(bridgeFolder, true);
        }
    }

    private static string CreateTempDirectory()
        => Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "grafcetstudio-bridge-path-" + Guid.NewGuid().ToString("N"))).FullName;

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


