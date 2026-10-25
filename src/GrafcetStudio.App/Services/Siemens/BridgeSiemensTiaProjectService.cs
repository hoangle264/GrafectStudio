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
    /// <summary>Exe path, or a folder holding GrafcetStudio.TiaBridge.V{n}.exe. A versioned variant (…_PATH_V20) wins over it.</summary>
    public const string BridgePathEnvironmentVariable = "GRAFCETSTUDIO_TIA_BRIDGE_PATH";
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(90);

    private readonly Func<string, string?> bridgePathProvider;
    private readonly TimeSpan timeout;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    public BridgeSiemensTiaProjectService()
        : this(tiaVersion => ResolveDefaultBridgePath(tiaVersion))
    {
    }

    public BridgeSiemensTiaProjectService(Func<string?> bridgePathProvider, TimeSpan? timeout = null)
        : this(_ => bridgePathProvider(), timeout)
    {
    }

    /// <param name="bridgePathProvider">Receives the canonical TIA version ("V19", "V20") and returns the bridge exe path.</param>
    public BridgeSiemensTiaProjectService(Func<string, string?> bridgePathProvider, TimeSpan? timeout = null)
    {
        this.bridgePathProvider = bridgePathProvider;
        this.timeout = timeout ?? DefaultTimeout;
    }

    public async Task<SiemensPushResult> PushBlockXmlAsync(SiemensPushRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var validationError = SiemensPushRequestHelper.ValidateRequest(request);
        if (validationError is not null)
        {
            return validationError;
        }

        var tempXmlPath = string.Empty;
        var tempRequestPath = string.Empty;
        try
        {
            var xmlPath = SiemensPushRequestHelper.PrepareXmlFile(request, out tempXmlPath);
            var bridgePath = ResolveBridgePath(request);
            if (bridgePath.Result is not null)
            {
                return bridgePath.Result;
            }

            var bridgeRequest = new TiaBridgeRequest
            {
                TiaVersion = SiemensTiaVersions.NormalizeOrDefault(request.TiaVersion),
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
                SiemensPushRequestHelper.TryDeleteFile(tempRequestPath, "Siemens TIA Bridge");
            }

            if (!string.IsNullOrWhiteSpace(tempXmlPath))
            {
                SiemensPushRequestHelper.TryDeleteFile(tempXmlPath, "Siemens TIA Bridge");
            }
        }
    }

    private async Task<SiemensPushResult> InvokeBridgeAsync(string bridgePath, string requestPath, SiemensPushRequest request, CancellationToken cancellationToken)
    {
        using var process = new Process();
        process.StartInfo = CreateBridgeProcessStartInfo(bridgePath, requestPath);

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
                $"TIA bridge executable was not found or could not be started at '{bridgePath}'. Set {BridgePathEnvironmentVariable} to a valid {SiemensTiaVersions.BridgeExecutableName(request.TiaVersion)} path. Details: {error.Message}",
                request);
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        var timedOut = await WaitForProcessExitWithTimeoutAsync(process, timeout, cancellationToken);
        if (timedOut)
        {
            TryKill(process);
            return SiemensPushResult.Failure(
                SiemensTiaProjectServiceStatus.Timeout,
                $"TIA bridge timed out after {timeout.TotalSeconds:0} seconds. Ensure TIA Portal is responsive and the target project is open or reachable.",
                request);
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        return ParseBridgeResponse(process.ExitCode, stdout, stderr, request);
    }

    private static ProcessStartInfo CreateBridgeProcessStartInfo(string bridgePath, string requestPath)
        => new()
        {
            FileName = bridgePath,
            Arguments = $"--request \"{requestPath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(bridgePath) ?? Environment.CurrentDirectory
        };

    private static async Task<bool> WaitForProcessExitWithTimeoutAsync(Process process, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
            return false;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return true;
        }
    }

    private static SiemensPushResult ParseBridgeResponse(int exitCode, string stdout, string stderr, SiemensPushRequest request)
    {
        if (string.IsNullOrWhiteSpace(stdout))
        {
            var message = exitCode == 0
                ? "TIA bridge returned empty stdout."
                : $"TIA bridge failed with exit code {exitCode} and returned empty stdout.";
            if (!string.IsNullOrWhiteSpace(stderr))
            {
                message += " Stderr: " + stderr.Trim();
            }

            return SiemensPushResult.Failure(MapExitCodeStatus(exitCode), message, request);
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

        var responseMessage = BuildUserMessage(bridgeResponse, exitCode);
        if (!string.IsNullOrWhiteSpace(bridgeResponse.Details))
        {
            responseMessage = string.IsNullOrWhiteSpace(responseMessage)
                ? bridgeResponse.Details
                : responseMessage + " Details: " + bridgeResponse.Details;
        }

        if (bridgeResponse.Success && exitCode == 0)
        {
            return SiemensPushResult.Success(responseMessage, request, bridgeResponse.ImportedPath ?? request.XmlPath);
        }

        if (exitCode != 0 && !string.IsNullOrWhiteSpace(stderr))
        {
            responseMessage += " Stderr: " + stderr.Trim();
        }

        return SiemensPushResult.Failure(MapBridgeStatus(bridgeResponse.Status, exitCode), responseMessage, request);
    }

    private BridgePathResolution ResolveBridgePath(SiemensPushRequest request)
    {
        var tiaVersion = SiemensTiaVersions.NormalizeOrDefault(request.TiaVersion);
        var executableName = SiemensTiaVersions.BridgeExecutableName(tiaVersion);
        var configured = bridgePathProvider(tiaVersion)?.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(configured))
        {
            return new BridgePathResolution(null, SiemensPushResult.Failure(
                SiemensTiaProjectServiceStatus.BridgeNotFound,
                $"TIA Portal {tiaVersion} bridge is not configured. Set {BridgePathEnvironmentVariable} or place {executableName} next to the app. The Siemens XML was generated successfully and can still be imported manually in TIA Portal.",
                request));
        }

        var fullPath = Path.GetFullPath(configured);
        if (!File.Exists(fullPath))
        {
            return new BridgePathResolution(null, SiemensPushResult.Failure(
                SiemensTiaProjectServiceStatus.BridgeNotFound,
                $"TIA Portal {tiaVersion} bridge executable was not found at '{fullPath}'. Set {BridgePathEnvironmentVariable} to a valid {executableName} path. The Siemens XML was generated successfully and remains available for manual TIA import.",
                request));
        }

        return new BridgePathResolution(fullPath, null);
    }

    public static string ResolveDefaultBridgePath(string tiaVersion)
        => ResolveDefaultBridgePath(tiaVersion, AppContext.BaseDirectory, Environment.GetEnvironmentVariable);

    /// <summary>Lookup order: env var (versioned, then generic) -> next to the app -> tia-bridge/ -> repo build output.</summary>
    public static string ResolveDefaultBridgePath(string tiaVersion, string baseDirectory, Func<string, string?> getEnvironmentVariable)
    {
        tiaVersion = SiemensTiaVersions.NormalizeOrDefault(tiaVersion);
        var executableName = SiemensTiaVersions.BridgeExecutableName(tiaVersion);

        foreach (var variable in new[] { BridgePathEnvironmentVariable + "_" + tiaVersion, BridgePathEnvironmentVariable })
        {
            var envPath = getEnvironmentVariable(variable)?.Trim().Trim('"');
            if (!string.IsNullOrWhiteSpace(envPath))
            {
                return Directory.Exists(envPath) ? Path.Combine(envPath, executableName) : envPath;
            }
        }

        var candidate = Path.Combine(baseDirectory, executableName);
        if (File.Exists(candidate))
        {
            return candidate;
        }

        var shippedCandidate = Path.Combine(baseDirectory, "tia-bridge", executableName);
        if (File.Exists(shippedCandidate))
        {
            return shippedCandidate;
        }

        var repoCandidate = Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "..", "..", "..", "GrafcetStudio.TiaBridge." + tiaVersion, "bin", "Debug", "net48", executableName));
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



