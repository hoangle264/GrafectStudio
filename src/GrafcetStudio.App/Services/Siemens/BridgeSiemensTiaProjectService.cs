using GrafcetStudio.TiaBridge.Contracts;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace GrafcetStudio.App.Services.Siemens;

public sealed class BridgeSiemensTiaProjectService : ISiemensTiaProjectService
{
    public const string ImportModeEnvironmentVariable = "GRAFCETSTUDIO_TIA_IMPORT_MODE";
    public const string BridgePathEnvironmentVariable = "GRAFCETSTUDIO_TIA_BRIDGE_PATH";
    public const string DefaultBridgeExecutableName = "GrafcetStudio.TiaBridge.V19.exe";
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(90);

    private readonly Func<string?> bridgePathProvider;
    private readonly TimeSpan timeout;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    public BridgeSiemensTiaProjectService()
        : this(() => ResolveDefaultBridgePath())
    {
    }

    public BridgeSiemensTiaProjectService(Func<string?> bridgePathProvider, TimeSpan? timeout = null)
    {
        this.bridgePathProvider = bridgePathProvider;
        this.timeout = timeout ?? DefaultTimeout;
    }

    public async Task<SiemensPushResult> PushBlockXmlAsync(SiemensPushRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var validationError = ValidateRequest(request);
        if (validationError is not null)
        {
            return validationError;
        }

        var tempXmlPath = string.Empty;
        var tempRequestPath = string.Empty;
        try
        {
            var xmlPath = PrepareXmlFile(request, out tempXmlPath);
            var bridgePath = ResolveBridgePath(request);
            if (bridgePath.Result is not null)
            {
                return bridgePath.Result;
            }

            var bridgeRequest = new TiaBridgeRequest
            {
                TiaVersion = "V19",
                ProjectPath = request.ProjectPath,
                DeviceName = request.DeviceName!,
                PlcName = request.PlcName!,
                TargetFolderPath = request.TargetFolderPath!,
                BlockName = request.BlockName!,
                XmlPath = xmlPath,
                OverwriteMode = MapOverwriteMode(request.OverwriteMode)
            };

            tempRequestPath = Path.Combine(Path.GetTempPath(), $"grafcetstudio-tia-request-{Guid.NewGuid():N}.json");
            await File.WriteAllTextAsync(tempRequestPath, JsonSerializer.Serialize(bridgeRequest, JsonOptions), cancellationToken);

            return await InvokeBridgeAsync(bridgePath.Path!, tempRequestPath, request, cancellationToken);
        }
        catch (FileNotFoundException error)
        {
            return SiemensPushResult.Failure(
                SiemensTiaProjectServiceStatus.InvalidRequest,
                $"Siemens XML file was not found. Regenerate the XML and try again. Details: {error.FileName ?? error.Message}",
                request);
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(tempRequestPath))
            {
                TryDeleteFile(tempRequestPath);
            }

            if (!string.IsNullOrWhiteSpace(tempXmlPath))
            {
                TryDeleteFile(tempXmlPath);
            }
        }
    }

    private async Task<SiemensPushResult> InvokeBridgeAsync(string bridgePath, string requestPath, SiemensPushRequest request, CancellationToken cancellationToken)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = bridgePath,
            Arguments = $"--request \"{requestPath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(bridgePath) ?? Environment.CurrentDirectory
        };

        try
        {
            if (!process.Start())
            {
                return SiemensPushResult.Failure(
                    SiemensTiaProjectServiceStatus.BridgeNotFound,
                    $"Could not start TIA bridge process '{bridgePath}'. Check {BridgePathEnvironmentVariable} and verify the bridge executable exists.",
                    request);
            }
        }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            return SiemensPushResult.Failure(
                SiemensTiaProjectServiceStatus.BridgeNotFound,
                $"TIA bridge executable was not found or could not be started at '{bridgePath}'. Set {BridgePathEnvironmentVariable} to a valid {DefaultBridgeExecutableName} path. Details: {error.Message}",
                request);
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            return SiemensPushResult.Failure(
                SiemensTiaProjectServiceStatus.Timeout,
                $"TIA bridge timed out after {timeout.TotalSeconds:0} seconds. Ensure TIA Portal is responsive and the target project is open or reachable.",
                request);
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (string.IsNullOrWhiteSpace(stdout))
        {
            var message = process.ExitCode == 0
                ? "TIA bridge returned empty stdout."
                : $"TIA bridge failed with exit code {process.ExitCode} and returned empty stdout.";
            if (!string.IsNullOrWhiteSpace(stderr))
            {
                message += " Stderr: " + stderr.Trim();
            }

            return SiemensPushResult.Failure(MapExitCodeStatus(process.ExitCode), message, request);
        }

        TiaBridgeResponse? bridgeResponse;
        try
        {
            bridgeResponse = JsonSerializer.Deserialize<TiaBridgeResponse>(stdout, JsonOptions);
        }
        catch (JsonException error)
        {
            var message = $"TIA bridge returned invalid JSON stdout. Details: {error.Message}";
            if (!string.IsNullOrWhiteSpace(stderr))
            {
                message += " Stderr: " + stderr.Trim();
            }

            return SiemensPushResult.Failure(SiemensTiaProjectServiceStatus.ImportFailed, message, request);
        }

        if (bridgeResponse is null)
        {
            return SiemensPushResult.Failure(
                SiemensTiaProjectServiceStatus.ImportFailed,
                "TIA bridge returned an empty JSON response.",
                request);
        }

        var responseMessage = BuildUserMessage(bridgeResponse, process.ExitCode);
        if (!string.IsNullOrWhiteSpace(bridgeResponse.Details))
        {
            responseMessage = string.IsNullOrWhiteSpace(responseMessage)
                ? bridgeResponse.Details
                : responseMessage + " Details: " + bridgeResponse.Details;
        }

        if (bridgeResponse.Success && process.ExitCode == 0)
        {
            return SiemensPushResult.Success(responseMessage, request, bridgeResponse.ImportedPath ?? request.XmlPath);
        }

        if (process.ExitCode != 0 && !string.IsNullOrWhiteSpace(stderr))
        {
            responseMessage += " Stderr: " + stderr.Trim();
        }

        return SiemensPushResult.Failure(MapBridgeStatus(bridgeResponse.Status, process.ExitCode), responseMessage, request);
    }

    private SiemensPushResult? ValidateRequest(SiemensPushRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceName))
        {
            return SiemensPushResult.Failure(SiemensTiaProjectServiceStatus.InvalidRequest, "DeviceName is required for TIA import.", request);
        }

        if (string.IsNullOrWhiteSpace(request.PlcName))
        {
            return SiemensPushResult.Failure(SiemensTiaProjectServiceStatus.InvalidRequest, "PlcName is required for TIA import.", request);
        }

        if (string.IsNullOrWhiteSpace(request.TargetFolderPath))
        {
            return SiemensPushResult.Failure(SiemensTiaProjectServiceStatus.InvalidRequest, "TargetFolderPath is required for TIA import.", request);
        }

        if (string.IsNullOrWhiteSpace(request.BlockName))
        {
            return SiemensPushResult.Failure(SiemensTiaProjectServiceStatus.InvalidRequest, "BlockName is required for TIA import.", request);
        }

        if (string.IsNullOrWhiteSpace(request.XmlContent) && string.IsNullOrWhiteSpace(request.XmlPath))
        {
            return SiemensPushResult.Failure(SiemensTiaProjectServiceStatus.InvalidRequest, "Either XmlContent or XmlPath is required for TIA import.", request);
        }

        return null;
    }

    private static string PrepareXmlFile(SiemensPushRequest request, out string tempXmlPath)
    {
        tempXmlPath = string.Empty;
        if (!string.IsNullOrWhiteSpace(request.XmlPath))
        {
            if (!File.Exists(request.XmlPath))
            {
                throw new FileNotFoundException("Siemens block XML file was not found.", request.XmlPath);
            }

            return Path.GetFullPath(request.XmlPath);
        }

        var safeBlockName = string.Concat((request.BlockName ?? "Grafcet_LAD").Select(ch => char.IsLetterOrDigit(ch) || ch is '_' or '-' ? ch : '_'));
        tempXmlPath = Path.Combine(Path.GetTempPath(), $"grafcetstudio-{safeBlockName}-{Guid.NewGuid():N}.xml");
        File.WriteAllText(tempXmlPath, request.XmlContent);
        return tempXmlPath;
    }

    private BridgePathResolution ResolveBridgePath(SiemensPushRequest request)
    {
        var configured = bridgePathProvider()?.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(configured))
        {
            return new BridgePathResolution(null, SiemensPushResult.Failure(
                SiemensTiaProjectServiceStatus.BridgeNotFound,
                $"TIA bridge is not configured. Set {BridgePathEnvironmentVariable} or place {DefaultBridgeExecutableName} next to the app. The Siemens XML was generated successfully and can still be imported manually in TIA Portal.",
                request));
        }

        var fullPath = Path.GetFullPath(configured);
        if (!File.Exists(fullPath))
        {
            return new BridgePathResolution(null, SiemensPushResult.Failure(
                SiemensTiaProjectServiceStatus.BridgeNotFound,
                $"TIA bridge executable was not found at '{fullPath}'. Set {BridgePathEnvironmentVariable} to a valid {DefaultBridgeExecutableName} path. The Siemens XML was generated successfully and remains available for manual TIA import.",
                request));
        }

        return new BridgePathResolution(fullPath, null);
    }

    private static string? ResolveDefaultBridgePath()
    {
        var envPath = Environment.GetEnvironmentVariable(BridgePathEnvironmentVariable)?.Trim();
        if (!string.IsNullOrWhiteSpace(envPath))
        {
            return envPath;
        }

        var baseDirectory = AppContext.BaseDirectory;
        var candidate = Path.Combine(baseDirectory, DefaultBridgeExecutableName);
        if (File.Exists(candidate))
        {
            return candidate;
        }

        var repoCandidate = Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "..", "..", "..", "GrafcetStudio.TiaBridge.V19", "bin", "Debug", "net48", DefaultBridgeExecutableName));
        return File.Exists(repoCandidate) ? repoCandidate : candidate;
    }

    private static TiaBridgeOverwriteMode MapOverwriteMode(SiemensOverwriteMode overwriteMode)
        => overwriteMode switch
        {
            SiemensOverwriteMode.Overwrite => TiaBridgeOverwriteMode.Overwrite,
            SiemensOverwriteMode.Rename => TiaBridgeOverwriteMode.Rename,
            _ => TiaBridgeOverwriteMode.FailIfExists
        };

    private static SiemensTiaProjectServiceStatus MapBridgeStatus(TiaBridgeStatus status, int exitCode)
        => status switch
        {
            TiaBridgeStatus.Success => SiemensTiaProjectServiceStatus.Imported,
            TiaBridgeStatus.InvalidRequest => SiemensTiaProjectServiceStatus.InvalidRequest,
            TiaBridgeStatus.ProjectNotFound => SiemensTiaProjectServiceStatus.ProjectNotFound,
            TiaBridgeStatus.DeviceNotFound => SiemensTiaProjectServiceStatus.DeviceNotFound,
            TiaBridgeStatus.PlcNotFound => SiemensTiaProjectServiceStatus.PlcNotFound,
            TiaBridgeStatus.TargetFolderNotFound => SiemensTiaProjectServiceStatus.TargetFolderNotFound,
            TiaBridgeStatus.ImportFailed => SiemensTiaProjectServiceStatus.ImportFailed,
            TiaBridgeStatus.XmlNotFound => SiemensTiaProjectServiceStatus.InvalidRequest,
            TiaBridgeStatus.Timeout => SiemensTiaProjectServiceStatus.Timeout,
            TiaBridgeStatus.BridgeNotFound => SiemensTiaProjectServiceStatus.BridgeNotFound,
            TiaBridgeStatus.TiaNotInstalled => SiemensTiaProjectServiceStatus.TiaOpennessUnavailable,
            TiaBridgeStatus.TiaAccessDenied => SiemensTiaProjectServiceStatus.TiaOpennessUnavailable,
            TiaBridgeStatus.UnexpectedError => MapExitCodeStatus(exitCode),
            _ => MapExitCodeStatus(exitCode)
        };

    private static SiemensTiaProjectServiceStatus MapExitCodeStatus(int exitCode)
        => exitCode switch
        {
            0 => SiemensTiaProjectServiceStatus.Imported,
            1 => SiemensTiaProjectServiceStatus.InvalidRequest,
            2 => SiemensTiaProjectServiceStatus.TiaOpennessUnavailable,
            3 => SiemensTiaProjectServiceStatus.ImportFailed,
            4 => SiemensTiaProjectServiceStatus.ImportFailed,
            _ => SiemensTiaProjectServiceStatus.ImportFailed
        };

    private static string BuildUserMessage(TiaBridgeResponse response, int exitCode)
        => response.Status switch
        {
            TiaBridgeStatus.Success => string.IsNullOrWhiteSpace(response.Message)
                ? "Siemens XML generated successfully and imported into TIA Portal."
                : response.Message,
            TiaBridgeStatus.BridgeNotFound => "TIA bridge is not available. The Siemens XML was generated successfully and can still be imported manually in TIA Portal.",
            TiaBridgeStatus.Timeout => "TIA bridge timed out while waiting for TIA Portal. Ensure TIA Portal is running, responsive, and the target project is open or reachable.",
            TiaBridgeStatus.TiaNotInstalled => "TIA Portal is not running or the target project could not be opened. Start TIA Portal or verify the configured project path.",
            TiaBridgeStatus.TiaAccessDenied => "TIA Openness access was denied. Add the current Windows user to the Siemens TIA Openness group and restart TIA Portal/GrafcetStudio.",
            TiaBridgeStatus.ProjectNotFound => "TIA project was not found. Open the project in TIA Portal or verify the configured project path.",
            TiaBridgeStatus.DeviceNotFound => "TIA device was not found. Verify the configured device name in the target project.",
            TiaBridgeStatus.PlcNotFound => "TIA PLC software was not found. Verify the configured PLC name and device hierarchy.",
            TiaBridgeStatus.TargetFolderNotFound => "TIA target block folder was not found. Create the folder in TIA Portal or adjust the configured folder path.",
            TiaBridgeStatus.ImportFailed => "TIA import failed after XML generation. Review the bridge details and validate the generated XML in TIA Portal.",
            TiaBridgeStatus.InvalidRequest => string.IsNullOrWhiteSpace(response.Message)
                ? "TIA bridge request is invalid. Review the configured TIA target fields and regenerate the XML."
                : response.Message,
            TiaBridgeStatus.XmlNotFound => "Siemens XML file was not found. Regenerate the XML and try again.",
            TiaBridgeStatus.UnexpectedError when exitCode == 0 => response.Message,
            _ => string.IsNullOrWhiteSpace(response.Message) ? "TIA bridge returned an unexpected result." : response.Message
        };

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            Trace.WriteLine($"[Siemens TIA Bridge] Could not delete temporary file '{path}'.");
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(true);
            }
        }
        catch
        {
            Trace.WriteLine("[Siemens TIA Bridge] Could not terminate timed out bridge process.");
        }
    }

    private sealed record BridgePathResolution(string? Path, SiemensPushResult? Result);
}



