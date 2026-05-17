using System.Threading.Tasks;

namespace GrafcetStudio.App.Services;

public interface IFileService
{
    Task SaveProjectAsync(string projectJson);
    Task<string?> OpenProjectAsync();
    Task ExportCodeAsync(string code, string platform);
}
