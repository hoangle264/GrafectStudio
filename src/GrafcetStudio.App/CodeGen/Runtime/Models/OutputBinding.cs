namespace GrafcetStudio.CodeGen.Runtime.Models;

public class OutputBinding
{
    public string PhysicalOutputRef { get; init; } = string.Empty;

    public string BindingMode { get; init; } = "normal";

    public string SourceExecuteBitRef { get; init; } = string.Empty;
}
