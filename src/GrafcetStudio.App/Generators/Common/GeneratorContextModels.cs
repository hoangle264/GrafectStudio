using GrafcetStudio.CodeGen.Runtime.Models;
using GrafcetStudio.Domain.Models;
using System.Collections.Generic;

namespace GrafcetStudio.App.Generators.Common;

public sealed record GeneratorContext
{
    public ProjectInfo? project { get; init; }
    public UnitContext unit { get; init; } = new();
    public IList<DeviceContext> devices { get; init; } = new List<DeviceContext>();
    public IList<ResolvedFlow> autoFlows { get; init; } = new List<ResolvedFlow>();
    public IList<ResolvedFlow> originFlows { get; init; } = new List<ResolvedFlow>();
    public IList<ResolvedFlow> macroFlows { get; init; } = new List<ResolvedFlow>();
    public IList<ResolvedFlow> macroStepFlows { get; init; } = new List<ResolvedFlow>();
    public IList<ResolvedFlow> flows { get; init; } = new List<ResolvedFlow>();
    public IList<MacroBindingContext> macroBindings { get; init; } = new List<MacroBindingContext>();
    public IList<MacroPortContext> macroPorts { get; init; } = new List<MacroPortContext>();
    public IList<DeviceOutputGroup> deviceOutputGroups { get; init; } = new List<DeviceOutputGroup>();
    public IList<OrchestratorContext> orchestrators { get; init; } = new List<OrchestratorContext>();
    public IList<string> warnings { get; init; } = new List<string>();
}

public sealed record UnitContext
{
    public string id { get; init; } = string.Empty;
    public string label { get; init; } = string.Empty;
    public int unitIndex { get; init; }
    public string stepMinAddress { get; init; } = string.Empty;
    public string stepMaxAddress { get; init; } = string.Empty;
    public DeviceContext? variable { get; init; }
}

public sealed record DeviceContext
{
    public string label { get; init; } = string.Empty;
    public string name { get; init; } = string.Empty;
    public string kind { get; init; } = "generic";
    public string format { get; init; } = string.Empty;
    public string? address { get; init; }
    public string partialName { get; init; } = string.Empty;
    public string standardPartialName { get; init; } = string.Empty;
    public IDictionary<string, string> signalAddresses { get; init; } = new Dictionary<string, string>();
    public IList<DeviceSignalContext>? signals { get; init; }
}

public sealed record DeviceSignalContext
{
    public string id { get; init; } = string.Empty;
    public string name { get; init; } = string.Empty;
    public string dataType { get; init; } = string.Empty;
    public string varType { get; init; } = string.Empty;
    public string? comment { get; init; }
    public string? address { get; init; }
}

public sealed record MacroPortContext
{
    public string unitId { get; init; } = string.Empty;
    public string callerFlowId { get; init; } = string.Empty;
    public string callerStepId { get; init; } = string.Empty;
    public string calleeFlowId { get; init; } = string.Empty;
    public string portName { get; init; } = string.Empty;
    public DeviceVariable? variable { get; init; }
}

/// <summary>
/// An orchestrator flow resolved into stages. The macros of one stage run in parallel;
/// the next stage starts once every macro of the stage reports Done.
/// </summary>
public sealed record OrchestratorContext
{
    public string id { get; init; } = string.Empty;
    public string name { get; init; } = string.Empty;
    public string controlState { get; init; } = string.Empty;
    public OrchestratorUnitContext unit { get; init; } = new();
    /// <summary>True when the last stage returns to the initial step, false when the sequence just ends.</summary>
    public bool loopsToStart { get; init; }
    public IList<OrchestratorStageContext> stages { get; init; } = new List<OrchestratorStageContext>();
    /// <summary>Every macro of every stage, in stage order.</summary>
    public IList<OrchestratorMacroContext> macros { get; init; } = new List<OrchestratorMacroContext>();
}

public sealed record OrchestratorUnitContext
{
    public string id { get; init; } = string.Empty;
    public string name { get; init; } = string.Empty;
    public string label { get; init; } = string.Empty;
}

public sealed record OrchestratorStageContext
{
    public int index { get; init; }
    public bool isFirst { get; init; }
    public bool isLast { get; init; }
    public bool isParallel { get; init; }
    public IList<OrchestratorMacroContext> macros { get; init; } = new List<OrchestratorMacroContext>();
    /// <summary>Macros of the stage that follows; the first stage when the orchestrator loops, empty when it ends.</summary>
    public IList<OrchestratorMacroContext> nextMacros { get; init; } = new List<OrchestratorMacroContext>();
}

public sealed record OrchestratorMacroContext
{
    public string stepId { get; init; } = string.Empty;
    public int stepNumber { get; init; }
    public string stepLabel { get; init; } = string.Empty;
    public string execAddress { get; init; } = string.Empty;
    public string doneAddress { get; init; } = string.Empty;
    public string macroStepId { get; init; } = string.Empty;
    public string macroStep { get; init; } = string.Empty;
    public OrchestratorPortContext port { get; init; } = new();
}

public sealed record OrchestratorPortContext
{
    public string label { get; init; } = string.Empty;
    /// <summary>Signal name (as defined in the MacroPort struct) to address.</summary>
    public IDictionary<string, string> signals { get; init; } = new Dictionary<string, string>();
}
