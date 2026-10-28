namespace GrafcetStudio.Domain.Models;

/// <summary>String values shared between the web payload and the generators.</summary>
public static class FlowConstants
{
    /// <summary><see cref="FlowInfo.Category"/> of a flow that sequences MacroSteps.</summary>
    public const string OrchestratorCategory = "orchestrator";

    /// <summary><see cref="DeviceVariable.Format"/> of the port variable of a MacroStep.</summary>
    public const string MacroPortFormat = "MacroPort";

    /// <summary><see cref="Step.Kind"/> of a step that calls a MacroStep flow.</summary>
    public const string MacroStepKind = "macro";

    /// <summary><see cref="FlowInfo.DiagramType"/> of a flow that is called by a macro step.</summary>
    public const string MacroStepDiagramType = "MacroStep";
}
