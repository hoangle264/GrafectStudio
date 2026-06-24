namespace GrafcetStudio.Domain.Models;

/// <summary>Template-friendly MacroStep call binding.</summary>
public class MacroBindingContext
{
    public string unitId { get; init; } = string.Empty;
    public string callerFlowId { get; init; } = string.Empty;
    public string callerStepId { get; init; } = string.Empty;
    public string calleeFlowId { get; init; } = string.Empty;
    public string portName { get; init; } = string.Empty;
}
