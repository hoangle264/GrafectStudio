using GrafcetStudio.TiaBridge.Contracts;
using Siemens.Engineering;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GrafcetStudio.TiaBridge.V19;

internal sealed class TiaV19ImportService
{
    public TiaBridgeResponse Import(TiaBridgeRequest request)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        try
        {
            var tiaPortal = AttachToRunningPortal(request) ?? OpenPortal(request);
            if (tiaPortal is null)
            {
                return Failure(TiaBridgeStatus.TiaNotInstalled,
                    "Could not attach to or open a TIA Portal V19 process. Ensure TIA Portal is installed, licensed, and Openness access is allowed for this Windows user.");
            }

            using (tiaPortal)
            {
                var project = ResolveProject(tiaPortal, request);
                if (project is null)
                {
                    return Failure(TiaBridgeStatus.ProjectNotFound,
                        $"TIA project was not found or could not be opened: '{request.ProjectPath}'. Open the project in TIA Portal or provide a valid project path.");
                }

                var device = FindDevice(project, request.DeviceName);
                if (device is null)
                {
                    return Failure(TiaBridgeStatus.DeviceNotFound,
                        $"TIA device was not found: '{request.DeviceName}'. Check the configured device name in the TIA project.");
                }

                var plcSoftware = FindPlcSoftware(device, request.PlcName);
                if (plcSoftware is null)
                {
                    return Failure(TiaBridgeStatus.PlcNotFound,
                        $"PLC software was not found for PLC '{request.PlcName}' on device '{request.DeviceName}'. Check the PLC name and device hierarchy.");
                }

                var blockFolder = ResolveBlockFolder(plcSoftware, request.TargetFolderPath);
                if (blockFolder is null)
                {
                    return Failure(TiaBridgeStatus.TargetFolderNotFound,
                        $"TIA block folder was not found: '{request.TargetFolderPath}'. Create the folder or adjust the target folder path.");
                }

                ImportBlock(blockFolder, request.XmlPath, request.OverwriteMode, request.BlockName);
                return Success($"Imported Siemens block XML '{request.BlockName}' into '{request.TargetFolderPath}'.", request.XmlPath, request.TiaVersion);
            }
        }
        catch (EngineeringSecurityException error)
        {
            return Failure(TiaBridgeStatus.TiaAccessDenied,
                "TIA Openness denied access. Add the current Windows user to the Siemens TIA Openness group and restart TIA Portal/GrafcetStudio.",
                error.Message);
        }
        catch (UnauthorizedAccessException error)
        {
            return Failure(TiaBridgeStatus.TiaAccessDenied,
                "TIA Openness or XML file access was denied. Check Windows permissions and TIA Openness user-group membership.",
                error.Message);
        }
        catch (FileNotFoundException error)
        {
            return Failure(TiaBridgeStatus.XmlNotFound,
                "XML file was not found.",
                error.FileName ?? error.Message);
        }
        catch (InvalidOperationException error)
        {
            return Failure(TiaBridgeStatus.ImportFailed,
                "TIA Openness import failed.",
                FlattenException(error));
        }
        catch (Exception error)
        {
            return Failure(TiaBridgeStatus.UnexpectedError,
                "Unexpected bridge error.",
                FlattenException(error));
        }
    }

    private static TiaPortal? AttachToRunningPortal(TiaBridgeRequest request)
    {
        foreach (var process in TiaPortal.GetProcesses())
        {
            try
            {
                return process.Attach();
            }
            catch (EngineeringSecurityException)
            {
                throw;
            }
            catch (Exception error)
            {
                WriteDebug($"Attach failed for process '{process.Id}': {error.Message}");
                if (string.IsNullOrWhiteSpace(request.ProjectPath))
                {
                    continue;
                }
            }
        }

        return null;
    }

    private static TiaPortal? OpenPortal(TiaBridgeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ProjectPath))
        {
            return null;
        }

        return new TiaPortal(TiaPortalMode.WithoutUserInterface);
    }

    private static Project? ResolveProject(TiaPortal tiaPortal, TiaBridgeRequest request)
    {
        foreach (var project in tiaPortal.Projects)
        {
            if (ProjectMatches(project, request.ProjectPath))
            {
                return project;
            }
        }

        if (string.IsNullOrWhiteSpace(request.ProjectPath))
        {
            return tiaPortal.Projects.FirstOrDefault();
        }

        if (!File.Exists(request.ProjectPath))
        {
            return null;
        }

        return tiaPortal.Projects.Open(new FileInfo(request.ProjectPath));
    }

    private static bool ProjectMatches(Project project, string? projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return true;
        }

        var actualPath = project.Path?.FullName;
        if (string.IsNullOrWhiteSpace(actualPath))
        {
            return false;
        }

        return string.Equals(Path.GetFullPath(actualPath), Path.GetFullPath(projectPath), StringComparison.OrdinalIgnoreCase)
            || string.Equals(actualPath, projectPath, StringComparison.OrdinalIgnoreCase);
    }

    private static Device? FindDevice(Project project, string deviceName)
        => project.Devices.FirstOrDefault(device => NameMatches(device.Name, deviceName));

    private static PlcSoftware? FindPlcSoftware(Device device, string plcName)
    {
        foreach (var deviceItem in device.DeviceItems)
        {
            var software = FindPlcSoftwareInDeviceItem(deviceItem, plcName);
            if (software is not null)
            {
                return software;
            }
        }

        return null;
    }

    private static PlcSoftware? FindPlcSoftwareInDeviceItem(DeviceItem deviceItem, string plcName)
    {
        var softwareContainer = deviceItem.GetService<SoftwareContainer>();
        if (softwareContainer?.Software is PlcSoftware plcSoftware)
        {
            if (string.IsNullOrWhiteSpace(plcName) || NameMatches(deviceItem.Name, plcName) || NameMatches(plcSoftware.Name, plcName))
            {
                return plcSoftware;
            }
        }

        foreach (var child in deviceItem.DeviceItems)
        {
            var nested = FindPlcSoftwareInDeviceItem(child, plcName);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private static object? ResolveBlockFolder(PlcSoftware plcSoftware, string targetFolderPath)
    {
        object? current = plcSoftware.BlockGroup;
        if (current is null)
        {
            return null;
        }

        var pathSegments = (targetFolderPath ?? string.Empty)
            .Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => segment.Trim())
            .Where(segment => !segment.Equals("Program blocks", StringComparison.OrdinalIgnoreCase)
                && !segment.Equals("ProgramBlocks", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (var segment in pathSegments)
        {
            current = FindNestedGroup(current, segment);
            if (current is null)
            {
                return null;
            }
        }

        return current;
    }

    private static object? FindNestedGroup(object current, string segment)
    {
        if (current is PlcBlockSystemGroup systemGroup)
        {
            return systemGroup.Groups.FirstOrDefault(group => NameMatches(group.Name, segment));
        }

        if (current is PlcBlockUserGroup userGroup)
        {
            return userGroup.Groups.FirstOrDefault(group => NameMatches(group.Name, segment));
        }

        return null;
    }

    private static void ImportBlock(object blockFolder, string xmlPath, TiaBridgeOverwriteMode overwriteMode, string blockName)
    {
        var importFile = new FileInfo(xmlPath);
        var importOption = MapImportOption(overwriteMode);

        if (blockFolder is PlcBlockSystemGroup systemGroup)
        {
            systemGroup.Blocks.Import(importFile, importOption);
        }
        else if (blockFolder is PlcBlockUserGroup userGroup)
        {
            userGroup.Blocks.Import(importFile, importOption);
        }
        else
        {
            throw new InvalidOperationException("Resolved target folder is not a supported PLC block group.");
        }

        WriteDebug($"Imported '{blockName}' from '{xmlPath}' with mode '{overwriteMode}'.");
    }

    private static ImportOptions MapImportOption(TiaBridgeOverwriteMode overwriteMode)
        => overwriteMode switch
        {
            TiaBridgeOverwriteMode.Overwrite => ImportOptions.Override,
            TiaBridgeOverwriteMode.Rename => ImportOptions.Override,
            _ => ImportOptions.None
        };

    private static bool NameMatches(string? actual, string? expected)
        => !string.IsNullOrWhiteSpace(actual)
            && !string.IsNullOrWhiteSpace(expected)
            && string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);

    private static TiaBridgeResponse Success(string message, string importedPath, string? tiaVersion)
        => new()
        {
            Success = true,
            Status = TiaBridgeStatus.Success,
            Message = message,
            ImportedPath = importedPath,
            TiaVersion = string.IsNullOrWhiteSpace(tiaVersion) ? "V19" : tiaVersion
        };

    private static TiaBridgeResponse Failure(TiaBridgeStatus status, string message, string? details = null)
        => new()
        {
            Success = false,
            Status = status,
            Message = message,
            Details = details,
            TiaVersion = "V19"
        };

    private static string FlattenException(Exception error)
    {
        var messages = new List<string>();
        for (var current = error; current is not null; current = current.InnerException)
        {
            if (!string.IsNullOrWhiteSpace(current.Message))
            {
                messages.Add(current.Message);
            }
        }

        return string.Join(" -> ", messages);
    }

    private static void WriteDebug(string message)
    {
        Console.Error.WriteLine("[TiaBridge.V19] " + message);
    }
}
