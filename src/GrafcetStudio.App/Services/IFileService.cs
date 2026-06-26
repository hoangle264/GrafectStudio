using GrafcetStudio.Domain.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GrafcetStudio.App.Services;

public interface IFileService
{
    Task SaveProjectAsync(string projectJson);
    Task<string?> OpenProjectAsync();
    Task ExportCodeAsync(IReadOnlyList<CodegenFile> files, string platform);
    Task<string?> BrowseDeviceLibraryPathAsync();
    Task<string?> BrowseTemplateRootPathAsync();
    Task<string?> BrowseOutputRootPathAsync();
    Task<string> ReadTemplateFileAsync(string rootPath, string relativePath);
}
