using System;
using System.Threading;
using System.Threading.Tasks;

namespace GrafcetStudio.App.Services.Siemens;

public interface ISiemensTiaProjectService
{
    Task<SiemensPushResult> PushBlockXmlAsync(SiemensPushRequest request, CancellationToken cancellationToken = default);
}

public sealed class SiemensPushRequest
{
    public string? ProjectPath { get; init; }
    public string? DeviceName { get; init; }
    public string? PlcName { get; init; }
    public string? TargetFolderPath { get; init; }
    public string? BlockName { get; init; }
    public string? XmlContent { get; init; }
    public string? XmlPath { get; init; }
    public SiemensOverwriteMode OverwriteMode { get; init; } = SiemensOverwriteMode.FailIfExists;
}

public sealed class SiemensPushResult
{
    public bool Ok { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? ProjectPath { get; init; }
    public string? DeviceName { get; init; }
    public string? PlcName { get; init; }
    public string? TargetFolderPath { get; init; }
    public string? BlockName { get; init; }
    public string? ImportedPath { get; init; }
    public SiemensTiaProjectServiceStatus Status { get; init; } = SiemensTiaProjectServiceStatus.Unknown;

    public static SiemensPushResult Failure(SiemensTiaProjectServiceStatus status, string message, SiemensPushRequest? request = null)
        => new()
        {
            Ok = false,
            Status = status,
            Message = message,
            ProjectPath = request?.ProjectPath,
            DeviceName = request?.DeviceName,
            PlcName = request?.PlcName,
            TargetFolderPath = request?.TargetFolderPath,
            BlockName = request?.BlockName
        };

    public static SiemensPushResult Success(string message, SiemensPushRequest request, string? importedPath = null)
        => new()
        {
            Ok = true,
            Status = SiemensTiaProjectServiceStatus.Imported,
            Message = message,
            ProjectPath = request.ProjectPath,
            DeviceName = request.DeviceName,
            PlcName = request.PlcName,
            TargetFolderPath = request.TargetFolderPath,
            BlockName = request.BlockName,
            ImportedPath = importedPath
        };
}

public enum SiemensOverwriteMode
{
    FailIfExists,
    Overwrite,
    Rename
}

public enum SiemensTiaProjectServiceStatus
{
    Unknown,
    Imported,
    TiaOpennessUnavailable,
    NotConfigured,
    InvalidRequest,
    ProjectNotFound,
    DeviceNotFound,
    PlcNotFound,
    TargetFolderNotFound,
    ImportFailed
}
