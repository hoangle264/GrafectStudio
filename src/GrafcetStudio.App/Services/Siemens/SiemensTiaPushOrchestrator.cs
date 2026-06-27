using GrafcetStudio.App.Events;
using GrafcetStudio.App.Generators;
using GrafcetStudio.CodeGen.Template;
using GrafcetStudio.Domain.Models;
using Prism.Events;
using System;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GrafcetStudio.App.Services.Siemens;

public sealed class SiemensTiaPushOrchestrator
{
    private readonly IEventAggregator events;
    private readonly ICodeGeneratorService codegen;
    private readonly ISiemensTiaProjectService tiaProjectService;
    private readonly IWebViewBridgeService bridge;
    private readonly ConfigService config;
    private readonly TemplateManager templateManager;

    private static readonly JsonSerializerOptions PayloadJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public SiemensTiaPushOrchestrator(IEventAggregator events, ICodeGeneratorService codegen, ISiemensTiaProjectService tiaProjectService, IWebViewBridgeService bridge, ConfigService config, TemplateManager templateManager)
    {
        this.events = events;
        this.codegen = codegen;
        this.tiaProjectService = tiaProjectService;
        this.bridge = bridge;
        this.config = config;
        this.templateManager = templateManager;
    }

    public void Init()
    {
        events.GetEvent<PushSiemensLadRequestedEvent>().Subscribe(OnRequested);
    }

    private async void OnRequested(PushSiemensLadPayload payload)
    {
        await PushAsync(payload);
    }

    public async Task<SiemensPushResult> PushAsync(PushSiemensLadPayload payload)
    {
        try
        {
            var codegenOutput = GenerateSiemensLadXml(payload);
            var xmlFile = SelectXmlFile(codegenOutput)
                ?? throw new InvalidOperationException("Siemens LAD generator did not return an XML file.");

            var request = new SiemensPushRequest
            {
                ProjectPath = payload.ProjectPath,
                DeviceName = payload.DeviceName,
                PlcName = payload.PlcName,
                TargetFolderPath = string.IsNullOrWhiteSpace(payload.TargetFolderPath) ? "Program blocks" : payload.TargetFolderPath,
                BlockName = ExtractBlockName(xmlFile),
                XmlContent = xmlFile.Content,
                OverwriteMode = ParseOverwriteMode(payload.OverwriteMode)
            };

            await config.SaveSiemensTiaConfigAsync(request.ProjectPath, request.DeviceName, request.PlcName, request.TargetFolderPath, request.OverwriteMode.ToString());
            var result = await tiaProjectService.PushBlockXmlAsync(request);
            await bridge.SendSiemensTiaPushResultAsync(new { kind = "result", result = ToBridgeResult(result) });
            return result;
        }
        catch (Exception error)
        {
            var result = SiemensPushResult.Failure(SiemensTiaProjectServiceStatus.ImportFailed, error.Message);
            await bridge.SendSiemensTiaPushResultAsync(new { kind = "result", result = ToBridgeResult(result) });
            return result;
        }
    }

    private CodegenOutput GenerateSiemensLadXml(PushSiemensLadPayload message)
    {
        var payload = JsonSerializer.Deserialize<CodegenPayload>(message.RawJson, PayloadJsonOptions)
            ?? throw new InvalidOperationException("Invalid Siemens LAD codegen payload.");

        if (string.IsNullOrWhiteSpace(payload.TemplateRootPath))
        {
            payload.TemplateRootPath = message.TemplatePath;
        }

        if (!string.IsNullOrWhiteSpace(payload.TemplateRootPath))
        {
            var loadResult = TemplateLoader.LoadFromPath(payload.TemplateRootPath);
            if (!loadResult.IsValid)
            {
                throw new InvalidOperationException("Template loader error: " + string.Join("; ", loadResult.Errors));
            }

            templateManager.RegisterTemplates(loadResult.Templates.Values.ToList());
        }

        var health = templateManager.ValidateHealth(null);
        if (!health.IsValid)
        {
            throw new InvalidOperationException("Template validation error: " + string.Join("; ", health.Errors));
        }

        payload.EnrichVariables();
        var platform = string.IsNullOrWhiteSpace(message.Platform) ? "siemens-lad" : message.Platform;
        if (!string.Equals(platform, "siemens-lad", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Siemens LAD push requires platform 'siemens-lad'.");
        }

        return codegen.Generate("siemens-lad", payload);
    }

    private static CodegenFile? SelectXmlFile(CodegenOutput output)
        => output.Files.FirstOrDefault(file => file.Path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            ?? output.Files.FirstOrDefault();

    private static string ExtractBlockName(CodegenFile file)
    {
        var content = file.Content ?? string.Empty;
        var match = Regex.Match(content, @"<AttributeList>[\s\S]*?<Name>([^<]+)</Name>", RegexOptions.IgnoreCase)
            .Groups.Cast<Group>().ElementAtOrDefault(1);
        if (match is not null && match.Success && !string.IsNullOrWhiteSpace(match.Value))
        {
            return match.Value.Trim();
        }

        var fileName = System.IO.Path.GetFileNameWithoutExtension(file.Path);
        return string.IsNullOrWhiteSpace(fileName) ? "Grafcet_LAD" : fileName;
    }

    private static SiemensOverwriteMode ParseOverwriteMode(string? value)
        => Enum.TryParse<SiemensOverwriteMode>(value, true, out var mode) ? mode : SiemensOverwriteMode.FailIfExists;

    private static object ToBridgeResult(SiemensPushResult result)
        => new
        {
            ok = result.Ok,
            message = result.Message,
            projectPath = result.ProjectPath,
            deviceName = result.DeviceName,
            plcName = result.PlcName,
            targetFolderPath = result.TargetFolderPath,
            blockName = result.BlockName,
            importedPath = result.ImportedPath,
            status = result.Status.ToString()
        };
}
