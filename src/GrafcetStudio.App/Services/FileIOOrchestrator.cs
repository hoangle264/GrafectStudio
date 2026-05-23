using GrafcetStudio.App.Events;
using Prism.Events;
using System;

namespace GrafcetStudio.App.Services;

public class FileIOOrchestrator
{
    private readonly IEventAggregator _events;
    private readonly IFileService _files;
    private readonly IWebViewBridgeService _bridge;

    public FileIOOrchestrator(IEventAggregator events, IFileService files, IWebViewBridgeService bridge)
    {
        _events = events;
        _files = files;
        _bridge = bridge;
    }

    public void Init()
    {
        _events.GetEvent<SaveFileRequestedEvent>().Subscribe(OnSaveRequested);
        _events.GetEvent<OpenFileRequestedEvent>().Subscribe(OnOpenRequested);
        _events.GetEvent<ExportCodeRequestedEvent>().Subscribe(OnExportRequested);
        _events.GetEvent<BrowseCodegenPathRequestedEvent>().Subscribe(OnBrowseCodegenPathRequested);
    }

    private async void OnSaveRequested(string projectJson)
    {
        try
        {
            await _files.SaveProjectAsync(projectJson);
        }
        catch (Exception ex)
        {
            await _bridge.SendErrorAsync("fileIO", ex.Message);
        }
    }

    private async void OnOpenRequested(object _)
    {
        try
        {
            var json = await _files.OpenProjectAsync();
            if (json is not null) await _bridge.LoadProjectDataAsync(json);
        }
        catch (Exception ex)
        {
            await _bridge.SendErrorAsync("fileIO", ex.Message);
        }
    }

    private async void OnExportRequested(ExportCodePayload payload)
    {
        try
        {
            await _files.ExportCodeAsync(payload.Code, payload.Platform);
        }
        catch (Exception ex)
        {
            await _bridge.SendErrorAsync("fileIO", ex.Message);
        }
    }

    private async void OnBrowseCodegenPathRequested(BrowseCodegenPathPayload payload)
    {
        try
        {
            var target = string.Equals(payload.Target, "deviceLibrary", StringComparison.OrdinalIgnoreCase)
                ? "deviceLibrary"
                : string.Equals(payload.Target, "outputRoot", StringComparison.OrdinalIgnoreCase)
                    ? "outputRoot"
                : "templateRoot";
            var path = target switch
            {
                "deviceLibrary" => await _files.BrowseDeviceLibraryPathAsync(),
                "outputRoot" => await _files.BrowseOutputRootPathAsync(),
                _ => await _files.BrowseTemplateRootPathAsync()
            };

            if (!string.IsNullOrWhiteSpace(path))
            {
                await _bridge.SendCodegenPathAsync(target, path);
            }
        }
        catch (Exception ex)
        {
            await _bridge.SendErrorAsync("fileIO", ex.Message);
        }
    }
}
