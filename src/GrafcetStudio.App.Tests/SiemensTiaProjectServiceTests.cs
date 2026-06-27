using GrafcetStudio.App.Services.Siemens;
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
        Assert.Equal(SiemensTiaProjectServiceStatus.TiaOpennessUnavailable, result.Status);
        Assert.Contains("TIA Openness push/import is not configured", result.Message);
        Assert.Contains("reflection or an external bridge", result.Message);
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
}
