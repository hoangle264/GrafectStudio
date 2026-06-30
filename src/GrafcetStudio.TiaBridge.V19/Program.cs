using GrafcetStudio.TiaBridge.Contracts;
using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GrafcetStudio.TiaBridge.V19;

internal static class Program
{
    private const string OpennessDirectoryEnvironmentVariable = "GRAFCETSTUDIO_TIA_OPENNESS_DIR";
    private const string EngineeringAssemblyName = "Siemens.Engineering.dll";
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

    private static int Main(string[] args)
    {
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
                    TiaVersion = "V19",
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
                    TiaVersion = "V19",
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
                    TiaVersion = "V19",
                    ExitCode = InvalidRequestExitCode
                }, InvalidRequestExitCode);
            }

            var validation = ValidateRequest(request);
            if (validation is not null)
            {
                return WriteResponse(validation.Response, validation.ExitCode);
            }

            var service = new TiaV19ImportService();
            var response = service.Import(request!);
            return WriteResponse(response, MapExitCode(response.Status));
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
                TiaVersion = "V19",
                ExitCode = UnexpectedErrorExitCode
            }, UnexpectedErrorExitCode);
        }
    }

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

        if (!string.Equals(requestedAssembly.Name, "Siemens.Engineering", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var configuredDirectory = Environment.GetEnvironmentVariable(OpennessDirectoryEnvironmentVariable)?.Trim().Trim('"');
        if (!string.IsNullOrWhiteSpace(configuredDirectory))
        {
            var configuredAssemblyPath = Path.Combine(configuredDirectory, EngineeringAssemblyName);
            if (File.Exists(configuredAssemblyPath))
            {
                return Assembly.LoadFrom(configuredAssemblyPath);
            }
        }

        var defaultAssemblyPath = @"C:\Program Files\Siemens\Automation\Portal V19\PublicAPI\V19\Siemens.Engineering.dll";
        if (File.Exists(defaultAssemblyPath))
        {
            return Assembly.LoadFrom(defaultAssemblyPath);
        }

        throw new FileNotFoundException(
            $"TIA Openness assembly '{EngineeringAssemblyName}' was not found. Install TIA Portal Openness V19 or set {OpennessDirectoryEnvironmentVariable} to the folder containing {EngineeringAssemblyName}.",
            !string.IsNullOrWhiteSpace(configuredDirectory)
                ? Path.Combine(configuredDirectory, EngineeringAssemblyName)
                : defaultAssemblyPath);
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
                TiaVersion = string.IsNullOrWhiteSpace(request.TiaVersion) ? "V19" : request.TiaVersion,
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
                TiaVersion = "V19",
                ExitCode = InvalidRequestExitCode
            }, InvalidRequestExitCode);
    }
}



