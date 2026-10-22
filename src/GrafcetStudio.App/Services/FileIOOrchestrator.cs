using GrafcetStudio.App.Events;
using Prism.Events;
using System;

namespace GrafcetStudio.App.Services;

public class FileIOOrchestrator
{
    private static readonly System.Text.Json.JsonSerializerOptions PreviewJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private readonly IEventAggregator _events;
    private readonly IFileService _files;
    private readonly IWebViewBridgeService _bridge;
    private readonly ConfigService _config;

    public FileIOOrchestrator(IEventAggregator events, IFileService files, IWebViewBridgeService bridge, ConfigService config)
    {
        _events = events;
        _files = files;
        _bridge = bridge;
        _config = config;
    }

    public void Init()
    {
        _events.GetEvent<SaveFileRequestedEvent>().Subscribe(OnSaveRequested);
        _events.GetEvent<OpenFileRequestedEvent>().Subscribe(OnOpenRequested);
        _events.GetEvent<ExportCodeRequestedEvent>().Subscribe(OnExportRequested);
        _events.GetEvent<BrowseCodegenPathRequestedEvent>().Subscribe(OnBrowseCodegenPathRequested);
        _events.GetEvent<ReadTemplateFileRequestedEvent>().Subscribe(OnReadTemplateFileRequested);
        _events.GetEvent<PreviewTemplateRequestedEvent>().Subscribe(OnPreviewTemplateRequested);
        _events.GetEvent<SetTemplateRootRequestedEvent>().Subscribe(OnSetTemplateRootRequested);
        _events.GetEvent<ListTemplateFilesRequestedEvent>().Subscribe(OnListTemplateFilesRequested);
        _events.GetEvent<ValidateTemplateRequestedEvent>().Subscribe(OnValidateTemplateRequested);
        _events.GetEvent<WriteTemplateFileRequestedEvent>().Subscribe(OnWriteTemplateFileRequested);
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
            await _files.ExportCodeAsync(payload.Files, payload.Platform);
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

    private async void OnReadTemplateFileRequested(ReadTemplateFilePayload payload)
    {
        try
        {
            var content = await _files.ReadTemplateFileAsync(payload.RootPath, payload.RelativePath);
            await _bridge.SendTemplateFileAsync(payload.RequestId, payload.RelativePath, content);
        }
        catch (Exception ex)
        {
            await _bridge.SendTemplateFileErrorAsync(payload.RequestId, payload.RelativePath, ex.Message);
        }
    }

    private async void OnPreviewTemplateRequested(PreviewTemplatePayload payload)
    {
        try
        {
            var codegen = System.Text.Json.JsonSerializer.Deserialize<GrafcetStudio.Domain.Models.CodegenPayload>(payload.RawJson, PreviewJsonOptions)
                          ?? throw new InvalidOperationException("Invalid codegen payload.");
            var editedPath = FileService.ResolveTemplatePath(payload.RootPath, payload.RelativePath);
            // Errors are caught inside the lambda: half-typed templates are expected while editing and
            // must not surface as unhandled exceptions on the worker thread.
            var (preview, error) = await System.Threading.Tasks.Task.Run<(GrafcetStudio.CodeGen.Template.TemplatePreviewResult? Preview, string? Error)>(() =>
            {
                var syntaxError = GrafcetStudio.CodeGen.Template.TemplateSyntaxChecker.Check(payload.Content);
                if (syntaxError is not null) return (null, "Handlebars: " + syntaxError);
                try
                {
                    return (GrafcetStudio.CodeGen.Template.TemplatePreviewService.Render(payload.RootPath.Trim(), editedPath, payload.Content, codegen), null);
                }
                catch (Exception ex)
                {
                    return (null, ex.Message);
                }
            });
            if (preview is null)
            {
                await _bridge.SendTemplateFileErrorAsync(payload.RequestId, payload.RelativePath, error ?? "Preview failed.");
                return;
            }

            await _bridge.SendTemplatePreviewAsync(payload.RequestId, preview);
        }
        catch (Exception ex)
        {
            await _bridge.SendTemplateFileErrorAsync(payload.RequestId, payload.RelativePath, ex.Message);
        }
    }

    private async void OnSetTemplateRootRequested(SetTemplateRootPayload payload)
    {
        try
        {
            await _config.SaveTemplateRootAsync(payload.Path);
        }
        catch (Exception ex)
        {
            await _bridge.SendErrorAsync("fileIO", ex.Message);
        }
    }

    private async void OnListTemplateFilesRequested(ListTemplateFilesPayload payload)
    {
        try
        {
            var files = GrafcetStudio.CodeGen.Template.TemplateCatalog.List(payload.RootPath);
            await _bridge.SendTemplateListAsync(payload.RequestId, files);
        }
        catch (Exception ex)
        {
            await _bridge.SendTemplateFileErrorAsync(payload.RequestId, string.Empty, ex.Message);
        }
    }

    private async void OnValidateTemplateRequested(ValidateTemplatePayload payload)
    {
        try
        {
            var error = GrafcetStudio.CodeGen.Template.TemplateSyntaxChecker.Check(payload.Content);
            await _bridge.SendTemplateValidationAsync(payload.RequestId, error);
        }
        catch (Exception ex)
        {
            await _bridge.SendTemplateFileErrorAsync(payload.RequestId, string.Empty, ex.Message);
        }
    }

    private async void OnWriteTemplateFileRequested(WriteTemplateFilePayload payload)
    {
        try
        {
            await _files.WriteTemplateFileAsync(payload.RootPath, payload.RelativePath, payload.Content);
            await _bridge.SendTemplateFileAsync(payload.RequestId, payload.RelativePath, payload.Content);
        }
        catch (Exception ex)
        {
            await _bridge.SendTemplateFileErrorAsync(payload.RequestId, payload.RelativePath, ex.Message);
        }
    }
}
