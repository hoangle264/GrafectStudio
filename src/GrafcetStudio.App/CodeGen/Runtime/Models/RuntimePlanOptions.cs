using GrafcetStudio.Domain.Models;

namespace GrafcetStudio.CodeGen.Runtime.Models;

public class RuntimePlanOptions
{
    public IList<DeviceVariable> Vars { get; init; } = new List<DeviceVariable>();

    public string UnitId { get; init; } = string.Empty;

    public string Mode { get; init; } = string.Empty;

    public IList<string> PrevDoneRefs { get; init; } = new List<string>();

    public string TransitionRef { get; init; } = string.Empty;

    public string ExecuteBitRef { get; init; } = string.Empty;

    public string DoneBitRef { get; init; } = string.Empty;
}

public class RuntimePreviewOptions
{
    public int BaseMr { get; init; }
}
