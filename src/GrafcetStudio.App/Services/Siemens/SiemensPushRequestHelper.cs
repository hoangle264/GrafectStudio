using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace GrafcetStudio.App.Services.Siemens;

internal static class SiemensPushRequestHelper
{
    public static SiemensPushResult? ValidateRequest(SiemensPushRequest request)
    {
        if (SiemensTiaVersions.Normalize(request.TiaVersion) is null)
        {
            return SiemensPushResult.Failure(SiemensTiaProjectServiceStatus.InvalidRequest, $"TIA version '{request.TiaVersion}' is not supported. Supported versions: {string.Join(", ", SiemensTiaVersions.Supported)}.", request);
        }

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

    public static string PrepareXmlFile(SiemensPushRequest request, out string tempXmlPath)
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

    public static void TryDeleteFile(string path, string logTag = "Siemens TIA")
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
            Trace.WriteLine($"[{logTag}] Could not delete temporary file '{path}'.");
        }
    }
}
