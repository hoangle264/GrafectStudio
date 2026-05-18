using GrafcetStudio.Domain.Models;

namespace GrafcetStudio.CodeGen;

public interface IProjectRepository
{
    DiagramState? LoadDiagramState(string diagId);

    DiagramMeta? GetDiagramMeta(string diagId);

    IList<DiagramMeta> GetAllDiagrams();

    IList<DeviceType> GetDeviceTypes();

    string ProjectName { get; }
}
