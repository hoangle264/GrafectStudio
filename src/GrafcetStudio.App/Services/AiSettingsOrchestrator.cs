using GrafcetStudio.App.Events;
using GrafcetStudio.App.Services.Ai;
using Prism.Events;

namespace GrafcetStudio.App.Services;

public class AiSettingsOrchestrator
{
    private readonly IWebViewBridgeService _bridge;
    private static readonly System.Net.Http.HttpClient HttpClient = new();
    private readonly AiKeyStore _keyStore;

    public AiSettingsOrchestrator(IEventAggregator eventAggregator, IWebViewBridgeService bridge, AiKeyStore keyStore)
    {
        _bridge = bridge;
        _keyStore = keyStore;
        eventAggregator.GetEvent<AiSettingsRequestedEvent>().Subscribe(async payload => await HandleAsync(payload));
    }

    private async Task HandleAsync(AiSettingsPayload payload)
    {
        if (payload.Action == "list")
        {
            await HandleListModelsAsync(payload);
            return;
        }

        string? error = null;
        try
        {
            switch (payload.Action)
            {
                case "save":
                    if (!string.IsNullOrWhiteSpace(payload.ApiKey)) _keyStore.Save(payload.ApiKey, payload.Model);
                    else _keyStore.SaveModel(payload.Model);
                    break;
                case "clear":
                    _keyStore.Clear();
                    break;
                case "get":
                    break;
                default:
                    error = "Unknown AI settings action.";
                    break;
            }
        }
        catch (Exception ex)
        {
            error = "Could not update AI settings: " + ex.Message;
        }

        var snapshot = _keyStore.GetSnapshot();
        await _bridge.SendAiSettingsAsync(snapshot.Configured, snapshot.Source, snapshot.Model, error);
    }

    private async Task HandleListModelsAsync(AiSettingsPayload payload)
    {
        var key = string.IsNullOrWhiteSpace(payload.ApiKey) ? _keyStore.ResolveKey().Key : payload.ApiKey.Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            await _bridge.SendAiModelsAsync(false, Array.Empty<string>(), string.Empty, "Enter an API key first.");
            return;
        }

        var result = await GeminiModelLister.ListAsync(HttpClient, key);
        await _bridge.SendAiModelsAsync(result.Ok, result.Models, result.Suggested, result.Ok ? null : result.Error);
    }
}
