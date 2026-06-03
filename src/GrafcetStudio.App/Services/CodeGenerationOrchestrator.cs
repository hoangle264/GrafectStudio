using GrafcetStudio.App.Events;
using GrafcetStudio.App.Generators;
using GrafcetStudio.CodeGen.Template;
using GrafcetStudio.Domain.Models;
using Prism.Events;
using System;
using System.Linq;
using System.Text.Json;

namespace GrafcetStudio.App.Services;

public class CodeGenerationOrchestrator
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private readonly IEventAggregator _events;
    private readonly ICodeGeneratorService _codegen;
    private readonly IWebViewBridgeService _bridge;
    private readonly ConfigService _config;
    private readonly TemplateManager _templateManager;

    public CodeGenerationOrchestrator(IEventAggregator events, ICodeGeneratorService codegen, IWebViewBridgeService bridge, ConfigService config, TemplateManager templateManager)
    {
        _events = events;
        _codegen = codegen;
        _bridge = bridge;
        _config = config;
        _templateManager = templateManager;
    }

    public void Init()
    {
        _events.GetEvent<GenerateCodeRequestedEvent>().Subscribe(OnRequested);
    }

    private async void OnRequested(GenerateCodePayload message)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<CodegenPayload>(message.RawJson, PayloadJsonOptions)
                          ?? throw new InvalidOperationException("Invalid codegen payload.");
            if (string.IsNullOrWhiteSpace(payload.TemplateRootPath))
            {
                payload.TemplateRootPath = message.TemplatePath;
            }

            if (!string.IsNullOrWhiteSpace(payload.TemplateRootPath))
            {
                var loadResult = TemplateLoader.LoadFromPath(payload.TemplateRootPath);

                if (!loadResult.IsValid)
                {
                    await _bridge.SendErrorAsync("template-loader", string.Join("; ", loadResult.Errors));
                    return;
                }

                try
                {
                    _templateManager.RegisterTemplates(loadResult.Templates.Values.ToList());
                }
                catch (Exception ex)
                {
                    await _bridge.SendErrorAsync("template-validation", ex.Message);
                    return;
                }

                if (loadResult.Warnings.Count > 0)
                {
                    Console.WriteLine($"[Template Loader] Warnings: {string.Join("; ", loadResult.Warnings)}");
                }
            }

            var health = _templateManager.ValidateHealth(null);
            if (!health.IsValid)
            {
                await _bridge.SendErrorAsync("template-validation", string.Join("; ", health.Errors));
                return;
            }

            await _config.SavePathsAsync(message.DevPath, message.TemplatePath, message.OutputPath);
            payload.EnrichVariables();
            var platform = string.IsNullOrWhiteSpace(payload.Platform) ? message.Platform : payload.Platform;
            var code = _codegen.Generate(platform, payload);
            await _bridge.SendGeneratedCodeAsync(code);
        }
        catch (Exception ex)
        {
            await _bridge.SendErrorAsync("codegen", ex.Message);
        }
    }
}
