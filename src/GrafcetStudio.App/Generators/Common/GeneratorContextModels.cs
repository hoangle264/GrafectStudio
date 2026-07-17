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
    /// <summary>All resolved flows (macroFlows + macroStepFlows). Suitable for template iteration.</summary>
    public IList<ResolvedFlow> flows { get; init; } = new List<ResolvedFlow>();
    public IList<MacroBindingContext> macroBindings { get; init; } = new List<MacroBindingContext>();
    public IList<MacroPortContext> macroPorts { get; init; } = new List<MacroPortContext>();
    public IList<DeviceOutputGroup> deviceOutputGroups { get; init; } = new List<DeviceOutputGroup>();
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
