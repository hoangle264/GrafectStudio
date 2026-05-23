using GrafcetStudio.App.Events;
using GrafcetStudio.App.Generators;
using GrafcetStudio.Domain.Models;
using Prism.Events;
using System;
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

    public CodeGenerationOrchestrator(IEventAggregator events, ICodeGeneratorService codegen, IWebViewBridgeService bridge, ConfigService config)
    {
        _events = events;
        _codegen = codegen;
        _bridge = bridge;
        _config = config;
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
            await _config.SavePathsAsync(message.DeviceLibraryPath, message.TemplatePath, message.OutputPath);
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

