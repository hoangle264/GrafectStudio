using GrafcetStudio.App.Events;
using GrafcetStudio.App.Generators;
using GrafcetStudio.App.Services;
using GrafcetStudio.App.Services.Siemens;
using GrafcetStudio.CodeGen.Template;
using GrafcetStudio.Domain.Models;
using HandlebarsDotNet;
using Microsoft.Web.WebView2.Wpf;
using Prism.Events;
using Xunit;

namespace GrafcetStudio.App.Tests;

public class SiemensTiaPushOrchestratorTests
{
    [Fact]
    public async Task PushAsync_GeneratesSiemensLadXmlBeforeCallingTiaService()
    {
        var codegen = new FakeCodeGeneratorService();
        var tia = new CapturingSiemensTiaProjectService();
        var bridge = new CapturingBridgeService();
        var orchestrator = new SiemensTiaPushOrchestrator(
            new EventAggregator(),
            codegen,
            tia,
            bridge,
            new ConfigService(Path.Combine(Path.GetTempPath(), "grafcetstudio-test-" + Guid.NewGuid().ToString("N"), "config.json")),
            new TemplateManager(Handlebars.Create()));

        var result = await orchestrator.PushAsync(new PushSiemensLadPayload
        {
            Platform = "siemens-lad",
            RawJson = "{\"platform\":\"siemens-lad\",\"project\":{\"name\":\"Demo\"}}",
            DeviceName = "PLC Station",
            PlcName = "PLC_1",
            TargetFolderPath = "Program blocks/Grafcet",
            OverwriteMode = "Overwrite"
        });

        Assert.True(result.Ok);
        Assert.Equal("siemens-lad", codegen.Platform);
        Assert.NotNull(tia.Request);
        Assert.Equal("PLC Station", tia.Request!.DeviceName);
        Assert.Equal("PLC_1", tia.Request.PlcName);
        Assert.Equal("Program blocks/Grafcet", tia.Request.TargetFolderPath);
        Assert.Equal("Main_Grafcet_LAD", tia.Request.BlockName);
        Assert.Equal(SiemensOverwriteMode.Overwrite, tia.Request.OverwriteMode);
        Assert.Contains("SW.Blocks.FC", tia.Request.XmlContent);
        Assert.NotNull(bridge.LastPushResult);
    }

    private sealed class FakeCodeGeneratorService : ICodeGeneratorService
    {
        public string? Platform { get; private set; }

        public CodegenOutput Generate(string platform, CodegenPayload data)
        {
            Platform = platform;
            return new CodegenOutput
            {
                Files = new List<CodegenFile>
                {
                    new()
                    {
                        Path = "Main_Grafcet_LAD.xml",
                        Content = "<SW.Blocks.FC><AttributeList><Name>Main_Grafcet_LAD</Name></AttributeList></SW.Blocks.FC>"
                    }
                }
            };
        }
    }

    private sealed class CapturingSiemensTiaProjectService : ISiemensTiaProjectService
    {
        public SiemensPushRequest? Request { get; private set; }

        public Task<SiemensPushResult> PushBlockXmlAsync(SiemensPushRequest request, CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult(SiemensPushResult.Success("Imported", request, "Main_Grafcet_LAD.xml"));
        }
    }

    private sealed class CapturingBridgeService : IWebViewBridgeService
    {
        public object? LastPushResult { get; private set; }

        public void Init(WebView2 webView) { }
        public Task SendGeneratedCodeAsync(CodegenOutput output) => Task.CompletedTask;
        public Task SendAiChunkAsync(string chunk) => Task.CompletedTask;
        public Task SendAiResponseAsync(string rawText) => Task.CompletedTask;
        public Task SendAiStreamEventAsync(string kind, string? text = null, bool done = false) => Task.CompletedTask;
        public Task SendErrorAsync(string source, string message) => Task.CompletedTask;
        public Task LoadProjectDataAsync(string json) => Task.CompletedTask;
        public Task UpdateDiagramStateAsync(string actionsJson) => Task.CompletedTask;
        public Task SendCodegenPathAsync(string target, string path) => Task.CompletedTask;
        public Task SendSavedPathsAsync(string deviceLibraryPath, string templatePath, string outputPath) => Task.CompletedTask;
        public Task SendTemplateFileAsync(string requestId, string relativePath, string content) => Task.CompletedTask;

        public Task SendSiemensTiaPushResultAsync(object result)
        {
            LastPushResult = result;
            return Task.CompletedTask;
        }
    }
}
