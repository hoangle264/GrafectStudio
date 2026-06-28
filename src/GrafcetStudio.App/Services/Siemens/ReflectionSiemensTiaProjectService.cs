using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace GrafcetStudio.App.Services.Siemens;


public sealed class ReflectionSiemensTiaProjectService : ISiemensTiaProjectService
{
    public const string OpennessDirectoryEnvironmentVariable = "GRAFCETSTUDIO_TIA_OPENNESS_DIR";
    public const string EngineeringAssemblyName = "Siemens.Engineering.dll";

    private readonly Func<string?> opennessDirectoryProvider;

    public ReflectionSiemensTiaProjectService()
        : this(() => Environment.GetEnvironmentVariable(OpennessDirectoryEnvironmentVariable))
    {
    }

    public ReflectionSiemensTiaProjectService(Func<string?> opennessDirectoryProvider)
    {
        this.opennessDirectoryProvider = opennessDirectoryProvider;
    }

    public Task<SiemensPushResult> PushBlockXmlAsync(SiemensPushRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(PushBlockXml(request, cancellationToken));
    }

    private SiemensPushResult PushBlockXml(SiemensPushRequest request, CancellationToken cancellationToken)
    {
        var validationError = ValidateRequest(request);
        if (validationError is not null) return validationError;

        var tempXmlPath = string.Empty;
        try
        {
            var xmlPath = PrepareXmlFile(request, out tempXmlPath);
            var assemblyResult = LoadEngineeringAssembly(request);
            if (!assemblyResult.Ok || assemblyResult.Assembly is null) return assemblyResult.Result!;

            var tiaPortal = AttachToRunningPortal(assemblyResult.Assembly, request, cancellationToken)
                ?? OpenPortal(assemblyResult.Assembly, request, cancellationToken);
            if (tiaPortal is null)
            {
                return SiemensPushResult.Failure(
                    SiemensTiaProjectServiceStatus.TiaOpennessUnavailable,
                    "Could not attach to or open a TIA Portal process. Ensure TIA Portal is installed, licensed, and Openness access is allowed for this Windows user.",
                    request);
            }

            using var portalScope = new ComScope(tiaPortal);
            var project = ResolveProject(tiaPortal, request, cancellationToken);
            if (project is null)
            {
                return SiemensPushResult.Failure(
                    SiemensTiaProjectServiceStatus.ProjectNotFound,
                    $"TIA project was not found or could not be opened: '{request.ProjectPath}'. Open the project in TIA Portal or provide a valid project path.",
                    request);
            }

            var device = FindByName(GetEnumerableProperty(project, "Devices"), request.DeviceName);
            if (device is null)
            {
                return SiemensPushResult.Failure(
                    SiemensTiaProjectServiceStatus.DeviceNotFound,
                    $"TIA device was not found: '{request.DeviceName}'. Check the configured device name in the TIA project.",
                    request);
            }

            var plcSoftware = FindPlcSoftware(device, request.PlcName);
            if (plcSoftware is null)
            {
                return SiemensPushResult.Failure(
                    SiemensTiaProjectServiceStatus.PlcNotFound,
                    $"PLC software was not found for PLC '{request.PlcName}' on device '{request.DeviceName}'. Check the PLC name and device hierarchy.",
                    request);
            }

            var blockFolder = ResolveBlockFolder(plcSoftware, request.TargetFolderPath);
            if (blockFolder is null)
            {
                return SiemensPushResult.Failure(
                    SiemensTiaProjectServiceStatus.TargetFolderNotFound,
                    $"TIA block folder was not found: '{request.TargetFolderPath}'. Create the folder or adjust the target folder path.",
                    request);
            }

            var importResult = ImportBlock(blockFolder, xmlPath, request);
            if (!importResult.Ok) return importResult;

            return SiemensPushResult.Success(
                $"Imported Siemens block XML '{request.BlockName}' into '{request.TargetFolderPath}'.",
                request,
                xmlPath);
        }
        catch (TargetInvocationException error) when (IsOpennessPermissionError(error))
        {
            return SiemensPushResult.Failure(
                SiemensTiaProjectServiceStatus.TiaOpennessUnavailable,
                "TIA Openness denied access. Add the current Windows user to the Siemens TIA Openness group and restart TIA Portal/GrafcetStudio. Details: " + FlattenException(error),
                request);
        }
        catch (UnauthorizedAccessException error)
        {
            return SiemensPushResult.Failure(
                SiemensTiaProjectServiceStatus.TiaOpennessUnavailable,
                "TIA Openness or XML file access was denied. Check Windows permissions and TIA Openness user-group membership. Details: " + error.Message,
                request);
        }
        catch (FileNotFoundException error)
        {
            return SiemensPushResult.Failure(
                SiemensTiaProjectServiceStatus.TiaOpennessUnavailable,
                "TIA Openness assembly or XML file was not found. Details: " + error.Message,
                request);
        }
        catch (Exception error) when (IsImportError(error))
        {
            return SiemensPushResult.Failure(
                SiemensTiaProjectServiceStatus.ImportFailed,
                "TIA Openness import failed. Details: " + FlattenException(error),
                request);
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(tempXmlPath))
            {
                TryDeleteFile(tempXmlPath);
            }
        }
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

    private AssemblyLoadResult LoadEngineeringAssembly(SiemensPushRequest request)
    {
        var configuredDirectory = opennessDirectoryProvider()?.Trim().Trim('"');
        if (!string.IsNullOrWhiteSpace(configuredDirectory))
        {
            var configuredAssembly = Path.Combine(configuredDirectory, EngineeringAssemblyName);
            if (!File.Exists(configuredAssembly))
            {
                return AssemblyLoadResult.Failure(SiemensPushResult.Failure(
                    SiemensTiaProjectServiceStatus.TiaOpennessUnavailable,
                    $"TIA Openness assembly was not found at '{configuredAssembly}'. Set {OpennessDirectoryEnvironmentVariable} to the folder containing {EngineeringAssemblyName}.",
                    request));
            }

            return AssemblyLoadResult.Success(Assembly.LoadFrom(configuredAssembly));
        }

        try
        {
            return AssemblyLoadResult.Success(Assembly.Load(new AssemblyName("Siemens.Engineering")));
        }
        catch (FileNotFoundException)
        {
            return AssemblyLoadResult.Failure(SiemensPushResult.Failure(
                SiemensTiaProjectServiceStatus.TiaOpennessUnavailable,
                $"TIA Openness assembly '{EngineeringAssemblyName}' was not found. Install TIA Portal Openness or set {OpennessDirectoryEnvironmentVariable} to the PublicAPI folder for your TIA version.",
                request));
        }
    }

    private static object? AttachToRunningPortal(Assembly engineeringAssembly, SiemensPushRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var processType = engineeringAssembly.GetType("Siemens.Engineering.TiaPortalProcess");
        if (processType is null) return null;

        var processes = InvokeStatic(processType, "GetProcesses") as IEnumerable;
        if (processes is null) return null;

        foreach (var process in processes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var portal = Invoke(process, "Attach");
                if (portal is not null) return portal;
            }
            catch
            {
                if (string.IsNullOrWhiteSpace(request.ProjectPath)) continue;
            }
        }

        return null;
    }

    private static object? OpenPortal(Assembly engineeringAssembly, SiemensPushRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(request.ProjectPath)) return null;

        var portalType = engineeringAssembly.GetType("Siemens.Engineering.TiaPortal");
        if (portalType is null) return null;

        var modeType = engineeringAssembly.GetType("Siemens.Engineering.TiaPortalMode");
        var mode = modeType is null ? null : Enum.Parse(modeType, "WithoutUserInterface");
        return mode is null ? Activator.CreateInstance(portalType) : Activator.CreateInstance(portalType, mode);
    }

    private static object? ResolveProject(object tiaPortal, SiemensPushRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var projects = GetEnumerableProperty(tiaPortal, "Projects");
        if (projects is not null)
        {
            foreach (var project in projects)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (ProjectMatches(project, request.ProjectPath)) return project;
            }
        }

        if (string.IsNullOrWhiteSpace(request.ProjectPath)) return projects?.Cast<object>().FirstOrDefault();

        var projectsObject = GetProperty(tiaPortal, "Projects");
        if (projectsObject is null) return null;

        var projectFile = CreateFileInfoArgument(projectsObject.GetType().Assembly, request.ProjectPath);
        return Invoke(projectsObject, "Open", projectFile);
    }

    private static bool ProjectMatches(object project, string? projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath)) return true;

        var path = GetProperty(project, "Path")?.ToString()
            ?? GetProperty(project, "ProjectPath")?.ToString();
        if (string.IsNullOrWhiteSpace(path)) return false;

        return string.Equals(Path.GetFullPath(path), Path.GetFullPath(projectPath), StringComparison.OrdinalIgnoreCase)
            || string.Equals(path, projectPath, StringComparison.OrdinalIgnoreCase);
    }

    private static object? FindPlcSoftware(object device, string? plcName)
    {
        foreach (var candidate in EnumerateDeviceItems(device))
        {
            if (!string.IsNullOrWhiteSpace(plcName) && !NameMatches(candidate, plcName))
            {
                var nested = FindPlcSoftwareInDeviceItem(candidate, plcName);
                if (nested is not null) return nested;
                continue;
            }

            var software = FindPlcSoftwareInDeviceItem(candidate, plcName);
            if (software is not null) return software;
        }

        return null;
    }

    private static object? FindPlcSoftwareInDeviceItem(object deviceItem, string? plcName)
    {
        var serviceProvider = deviceItem;
        var softwareContainer = GetService(serviceProvider, "Siemens.Engineering.HW.Features.SoftwareContainer");
        var software = GetProperty(softwareContainer, "Software");
        if (software is not null && TypeNameContains(software, "PlcSoftware")) return software;

        foreach (var child in GetEnumerableProperty(deviceItem, "DeviceItems") ?? Array.Empty<object>())
        {
            if (string.IsNullOrWhiteSpace(plcName) || NameMatches(child, plcName))
            {
                var nested = FindPlcSoftwareInDeviceItem(child, plcName);
                if (nested is not null) return nested;
            }
        }

        return null;
    }

    private static IEnumerable EnumerateDeviceItems(object device)
        => GetEnumerableProperty(device, "DeviceItems") ?? Array.Empty<object>();

    private static object? ResolveBlockFolder(object plcSoftware, string? targetFolderPath)
    {
        var blockGroup = GetProperty(plcSoftware, "BlockGroup") ?? GetProperty(plcSoftware, "Blocks");
        if (blockGroup is null) return null;

        var path = (targetFolderPath ?? string.Empty)
            .Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(segment => !segment.Equals("Program blocks", StringComparison.OrdinalIgnoreCase)
                && !segment.Equals("ProgramBlocks", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var current = blockGroup;
        foreach (var segment in path)
        {
            var groups = GetEnumerableProperty(current, "Groups") ?? GetEnumerableProperty(current, "Group") ?? GetEnumerableProperty(current, "Folders");
            current = FindByName(groups, segment);
            if (current is null) return null;
        }

        return current;
    }

    private static SiemensPushResult ImportBlock(object blockFolder, string xmlPath, SiemensPushRequest request)
    {
        try
        {
            var importOptions = ResolveImportOptions(blockFolder.GetType().Assembly, request.OverwriteMode);
            var fileInfoArgument = CreateFileInfoArgument(blockFolder.GetType().Assembly, xmlPath);

            object? result = null;
            if (importOptions is not null)
            {
                result = Invoke(blockFolder, "Import", fileInfoArgument, importOptions);
            }
            else
            {
                result = Invoke(blockFolder, "Import", fileInfoArgument);
            }

            Trace.WriteLine($"[Siemens TIA] Import result for '{request.BlockName}': {result ?? "completed"}");
            return SiemensPushResult.Success($"Imported Siemens block XML '{request.BlockName}'.", request, xmlPath);
        }
        catch (Exception error)
        {
            return SiemensPushResult.Failure(
                SiemensTiaProjectServiceStatus.ImportFailed,
                "TIA Openness import XML failed. Details: " + FlattenException(error),
                request);
        }
    }

    private static object? ResolveImportOptions(Assembly assembly, SiemensOverwriteMode overwriteMode)
    {
        var importOptionsType = assembly.GetType("Siemens.Engineering.ImportOptions")
            ?? assembly.GetType("Siemens.Engineering.ImportOptionsEnum");
        if (importOptionsType is null || !importOptionsType.IsEnum) return null;

        var desired = overwriteMode switch
        {
            SiemensOverwriteMode.Overwrite => new[] { "Override", "Overwrite" },
            SiemensOverwriteMode.Rename => new[] { "Rename", "GenerateUniqueName" },
            _ => new[] { "None" }
        };

        foreach (var name in desired)
        {
            if (Enum.IsDefined(importOptionsType, name)) return Enum.Parse(importOptionsType, name);
        }

        return Enum.GetValues(importOptionsType).GetValue(0);
    }

    private static object? GetService(object? serviceProvider, string serviceTypeName)
    {
        if (serviceProvider is null) return null;
        var type = serviceProvider.GetType().Assembly.GetType(serviceTypeName)
            ?? AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(serviceTypeName)).FirstOrDefault(type => type is not null);
        if (type is null) return null;

        return Invoke(serviceProvider, "GetService", type);
    }

    private static object? FindByName(IEnumerable? items, string? name)
    {
        if (items is null) return null;
        foreach (var item in items)
        {
            if (NameMatches(item, name)) return item;
        }

        return null;
    }

    private static bool NameMatches(object? value, string? name)
    {
        if (value is null || string.IsNullOrWhiteSpace(name)) return false;
        var actual = GetProperty(value, "Name")?.ToString()
            ?? GetProperty(value, "DeviceName")?.ToString();
        return string.Equals(actual, name, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TypeNameContains(object value, string text)
        => value.GetType().FullName?.Contains(text, StringComparison.OrdinalIgnoreCase) == true;

    private static object? GetProperty(object? target, string name)
        => target?.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public)?.GetValue(target);

    private static IEnumerable? GetEnumerableProperty(object? target, string name)
        => GetProperty(target, name) as IEnumerable;

    private static object? Invoke(object target, string methodName, params object?[] args)
    {
        var method = target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.Name == methodName)
            .FirstOrDefault(method => ParametersMatch(method.GetParameters(), args));
        return method?.Invoke(target, args);
    }

    private static object? InvokeStatic(Type type, string methodName, params object?[] args)
    {
        var method = type.GetMethods(BindingFlags.Static | BindingFlags.Public)
            .Where(method => method.Name == methodName)
            .FirstOrDefault(method => ParametersMatch(method.GetParameters(), args));
        return method?.Invoke(null, args);
    }

    private static bool ParametersMatch(ParameterInfo[] parameters, object?[] args)
    {
        if (parameters.Length != args.Length) return false;
        for (var index = 0; index < parameters.Length; index++)
        {
            if (args[index] is null) continue;
            if (!parameters[index].ParameterType.IsInstanceOfType(args[index])) return false;
        }

        return true;
    }

    private static object CreateFileInfoArgument(Assembly assembly, string path)
    {
        var engineeringFileInfoType = assembly.GetType("Siemens.Engineering.FileInfo");
        if (engineeringFileInfoType is not null)
        {
            return Activator.CreateInstance(engineeringFileInfoType, path)
                ?? throw new InvalidOperationException("Could not create Siemens.Engineering.FileInfo for XML import.");
        }

        return new FileInfo(path);
    }

    private static bool IsOpennessPermissionError(Exception error)
    {
        var text = FlattenException(error);
        return text.Contains("Openness", StringComparison.OrdinalIgnoreCase)
            && (text.Contains("access", StringComparison.OrdinalIgnoreCase)
                || text.Contains("permission", StringComparison.OrdinalIgnoreCase)
                || text.Contains("denied", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsImportError(Exception error)
        => error is InvalidOperationException or TargetInvocationException or ArgumentException;

    private static string FlattenException(Exception error)
    {
        var messages = new List<string>();
        for (var current = error; current is not null; current = current.InnerException!)
        {
            messages.Add(current.Message);
        }

        return string.Join(" -> ", messages.Where(message => !string.IsNullOrWhiteSpace(message)));
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            Trace.WriteLine($"[Siemens TIA] Could not delete temporary XML file '{path}'.");
        }
    }

    private sealed class AssemblyLoadResult
    {
        private AssemblyLoadResult(Assembly? assembly, SiemensPushResult? result)
        {
            Assembly = assembly;
            Result = result;
        }

        public bool Ok => Assembly is not null;
        public Assembly? Assembly { get; }
        public SiemensPushResult? Result { get; }

        public static AssemblyLoadResult Success(Assembly assembly) => new(assembly, null);
        public static AssemblyLoadResult Failure(SiemensPushResult result) => new(null, result);
    }

    private sealed class ComScope : IDisposable
    {
        private readonly object instance;

        public ComScope(object instance)
        {
            this.instance = instance;
        }

        public void Dispose()
        {
            if (instance is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
