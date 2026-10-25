using GrafcetStudio.TiaBridge.Contracts;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GrafcetStudio.TiaBridge;

internal static class Program
{
    private const string OpennessDirectoryEnvironmentVariable = "GRAFCETSTUDIO_TIA_OPENNESS_DIR";
    private const string EngineeringAssemblyPrefix = "Siemens.Engineering";
    private const int SuccessExitCode = 0;
    private const int InvalidRequestExitCode = 1;
    private const int TiaUnavailableExitCode = 2;
    private const int TargetNotFoundExitCode = 3;
    private const int ImportFailedExitCode = 4;
    private const int UnexpectedErrorExitCode = 9;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    private static string VersionedOpennessDirectoryEnvironmentVariable => OpennessDirectoryEnvironmentVariable + "_" + BridgeVersion.TiaVersion;

    private static int Main(string[] args)
    {
        // Must run before any Siemens.Engineering type is touched, so Openness is loaded from the TIA install.
        AppDomain.CurrentDomain.AssemblyResolve += ResolveEngineeringAssembly;

        try
        {
            var requestPath = TryGetRequestPath(args);
            if (string.IsNullOrWhiteSpace(requestPath))
            {
                return WriteResponse(new TiaBridgeResponse
                {
                    Success = false,
                    Status = TiaBridgeStatus.InvalidRequest,
                    Message = "Missing required '--request <path>' argument.",
                    TiaVersion = BridgeVersion.TiaVersion,
                    ExitCode = InvalidRequestExitCode
                }, InvalidRequestExitCode);
            }

            if (!File.Exists(requestPath))
            {
                return WriteResponse(new TiaBridgeResponse
                {
                    Success = false,
                    Status = TiaBridgeStatus.InvalidRequest,
                    Message = "Request JSON file was not found.",
                    Details = requestPath,
                    TiaVersion = BridgeVersion.TiaVersion,
                    ExitCode = InvalidRequestExitCode
                }, InvalidRequestExitCode);
            }

            TiaBridgeRequest? request;
            try
            {
                var json = File.ReadAllText(requestPath);
                request = JsonSerializer.Deserialize<TiaBridgeRequest>(json, JsonOptions);
            }
            catch (JsonException error)
            {
                return WriteResponse(new TiaBridgeResponse
                {
                    Success = false,
                    Status = TiaBridgeStatus.InvalidRequest,
                    Message = "Request JSON is invalid.",
                    Details = error.Message,
                    TiaVersion = BridgeVersion.TiaVersion,
                    ExitCode = InvalidRequestExitCode
                }, InvalidRequestExitCode);
            }

            var validation = ValidateRequest(request);
            if (validation is not null)
            {
                return WriteResponse(validation.Response, validation.ExitCode);
            }

#if TIA_OPENNESS_MISSING
            return WriteResponse(new TiaBridgeResponse
            {
                Success = false,
                Status = TiaBridgeStatus.TiaNotInstalled,
                Message = $"This bridge was built without TIA Portal {BridgeVersion.TiaVersion} Openness (Siemens.Engineering.dll was not found at build time). Rebuild GrafcetStudio.TiaBridge.{BridgeVersion.TiaVersion} on a machine with TIA Portal {BridgeVersion.TiaVersion} Openness installed.",
                TiaVersion = BridgeVersion.TiaVersion,
                ExitCode = TiaUnavailableExitCode
            }, TiaUnavailableExitCode);
#else
            TiaBridgeResponse response;
            try
            {
                response = RunImport(request!);
            }
            catch (Exception error) when (IsOpennessAssemblyLoadFailure(error))
            {
                return WriteResponse(new TiaBridgeResponse
                {
                    Success = false,
                    Status = TiaBridgeStatus.TiaNotInstalled,
                    Message = $"TIA Openness {BridgeVersion.TiaVersion} assemblies could not be loaded. Install TIA Portal {BridgeVersion.TiaVersion} Openness or set {VersionedOpennessDirectoryEnvironmentVariable} to the folder containing Siemens.Engineering.dll.",
                    Details = error.Message,
                    TiaVersion = BridgeVersion.TiaVersion,
                    ExitCode = TiaUnavailableExitCode
                }, TiaUnavailableExitCode);
            }

            return WriteResponse(response, MapExitCode(response.Status));
#endif
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error.ToString());
            return WriteResponse(new TiaBridgeResponse
            {
                Success = false,
                Status = TiaBridgeStatus.UnexpectedError,
                Message = "Unexpected bridge error.",
                Details = error.ToString(),
                TiaVersion = BridgeVersion.TiaVersion,
                ExitCode = UnexpectedErrorExitCode
            }, UnexpectedErrorExitCode);
        }
    }

#if !TIA_OPENNESS_MISSING
    // Kept out of Main so the JIT does not need Siemens.Engineering before AssemblyResolve is registered.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static TiaBridgeResponse RunImport(TiaBridgeRequest request)
        => new TiaImportService().Import(request);
#endif

    internal static bool IsOpennessAssemblyLoadFailure(Exception error)
        => error switch
        {
            FileNotFoundException notFound => IsOpennessAssemblyName(notFound.FileName),
            FileLoadException loadFailed => IsOpennessAssemblyName(loadFailed.FileName),
            BadImageFormatException badImage => IsOpennessAssemblyName(badImage.FileName),
            _ => false
        };

    private static bool IsOpennessAssemblyName(string? fileName)
        => !string.IsNullOrWhiteSpace(fileName)
            && fileName!.IndexOf(EngineeringAssemblyPrefix, StringComparison.OrdinalIgnoreCase) >= 0;

    private static Assembly? ResolveEngineeringAssembly(object? sender, ResolveEventArgs args)
    {
        AssemblyName requestedAssembly;
        try
        {
            requestedAssembly = new AssemblyName(args.Name);
        }
        catch
        {
            return null;
        }

        if (requestedAssembly.Name is null
            || !requestedAssembly.Name.StartsWith(EngineeringAssemblyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        foreach (var directory in GetOpennessDirectories())
        {
            var assemblyPath = Path.Combine(directory, requestedAssembly.Name + ".dll");
            if (File.Exists(assemblyPath) && MatchesMajorVersion(assemblyPath, requestedAssembly.Version))
            {
                return Assembly.LoadFrom(assemblyPath);
            }
        }

        return null;
    }

    // Same order as the build: versioned env var -> default install -> generic env var.
    // The generic env var may point at another TIA version; MatchesMajorVersion filters that out.
    private static IEnumerable<string> GetOpennessDirectories()
    {
        var versioned = Environment.GetEnvironmentVariable(VersionedOpennessDirectoryEnvironmentVariable)?.Trim().Trim('"');
        if (!string.IsNullOrWhiteSpace(versioned))
        {
            yield return versioned!;
        }

        yield return $@"C:\Program Files\Siemens\Automation\Portal {BridgeVersion.TiaVersion}\PublicAPI\{BridgeVersion.TiaVersion}";

        var generic = Environment.GetEnvironmentVariable(OpennessDirectoryEnvironmentVariable)?.Trim().Trim('"');
        if (!string.IsNullOrWhiteSpace(generic))
        {
            yield return generic!;
        }
    }

    private static bool MatchesMajorVersion(string assemblyPath, Version? requestedVersion)
    {
        if (requestedVersion is null)
        {
            return true;
        }

        try
        {
            return AssemblyName.GetAssemblyName(assemblyPath).Version?.Major == requestedVersion.Major;
        }
        catch
        {
            return false;
        }
    }

    private static string? TryGetRequestPath(string[] args)
    {
        if (args is null || args.Length == 0)
        {
            return null;
        }

        for (var index = 0; index < args.Length; index++)
        {
            if (!string.Equals(args[index], "--request", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var valueIndex = index + 1;
            return valueIndex < args.Length ? args[valueIndex] : null;
        }

        return null;
    }

    private static ValidationFailure? ValidateRequest(TiaBridgeRequest? request)
    {
        if (request is null)
        {
            return ValidationFailure.Invalid("Request payload is required.");
        }

        if (!string.IsNullOrWhiteSpace(request.TiaVersion)
            && !string.Equals(request.TiaVersion.Trim(), BridgeVersion.TiaVersion, StringComparison.OrdinalIgnoreCase))
        {
            return ValidationFailure.Invalid(
                $"Request targets TIA Portal {request.TiaVersion.Trim()} but this is the TIA Portal {BridgeVersion.TiaVersion} bridge. Use GrafcetStudio.TiaBridge.{request.TiaVersion.Trim().ToUpperInvariant()}.exe.");
        }

        if (string.IsNullOrWhiteSpace(request.DeviceName))
        {
            return ValidationFailure.Invalid("DeviceName is required.");
        }

        if (string.IsNullOrWhiteSpace(request.PlcName))
        {
            return ValidationFailure.Invalid("PlcName is required.");
        }

        if (string.IsNullOrWhiteSpace(request.TargetFolderPath))
        {
            return ValidationFailure.Invalid("TargetFolderPath is required.");
        }

        if (string.IsNullOrWhiteSpace(request.BlockName))
        {
            return ValidationFailure.Invalid("BlockName is required.");
        }

        if (string.IsNullOrWhiteSpace(request.XmlPath))
        {
            return ValidationFailure.Invalid("XmlPath is required.");
        }

        if (!File.Exists(request.XmlPath))
        {
            return new ValidationFailure(new TiaBridgeResponse
            {
                Success = false,
                Status = TiaBridgeStatus.XmlNotFound,
                Message = "XML file was not found.",
                Details = request.XmlPath,
                TiaVersion = BridgeVersion.TiaVersion,
                ExitCode = InvalidRequestExitCode
            }, InvalidRequestExitCode);
        }

        return null;
    }

    private static int MapExitCode(TiaBridgeStatus status)
        => status switch
        {
            TiaBridgeStatus.Success => SuccessExitCode,
            TiaBridgeStatus.InvalidRequest => InvalidRequestExitCode,
            TiaBridgeStatus.XmlNotFound => InvalidRequestExitCode,
            TiaBridgeStatus.TiaNotInstalled => TiaUnavailableExitCode,
            TiaBridgeStatus.TiaAccessDenied => TiaUnavailableExitCode,
            TiaBridgeStatus.ProjectNotFound => TargetNotFoundExitCode,
            TiaBridgeStatus.DeviceNotFound => TargetNotFoundExitCode,
            TiaBridgeStatus.PlcNotFound => TargetNotFoundExitCode,
            TiaBridgeStatus.TargetFolderNotFound => TargetNotFoundExitCode,
            TiaBridgeStatus.ImportFailed => ImportFailedExitCode,
            _ => UnexpectedErrorExitCode
        };

    private static int WriteResponse(TiaBridgeResponse response, int exitCode)
    {
        response.ExitCode = exitCode;
        Console.Out.WriteLine(JsonSerializer.Serialize(response, JsonOptions));
        return exitCode;
    }

    private sealed class ValidationFailure
    {
        public ValidationFailure(TiaBridgeResponse response, int exitCode)
        {
            Response = response;
            ExitCode = exitCode;
        }

        public TiaBridgeResponse Response { get; }

        public int ExitCode { get; }

        public static ValidationFailure Invalid(string message)
            => new(new TiaBridgeResponse
            {
                Success = false,
                Status = TiaBridgeStatus.InvalidRequest,
                Message = message,
                TiaVersion = BridgeVersion.TiaVersion,
                ExitCode = InvalidRequestExitCode
            }, InvalidRequestExitCode);
    }
}
