using GrafcetStudio.Domain.Models;

namespace GrafcetStudio.App.Tests;

public static class GrafcetStudioCodegenPayload
{
    public sealed class CodegenAssets
    {
        public string DeviceLibraryPath { get; init; } = string.Empty;
        public string TemplateRootPath { get; init; } = string.Empty;
        public string OutputPath { get; init; } = string.Empty;
        public string TemplateProfile { get; init; } = "simple";
    }

    public interface PayloadContext
    {
        Project project { get; }
        StoredDiagramData? loadDiagramData(string diagramId);
        CodegenAssets getAssets();
        object? ensureFlowAddressConfig(DiagramMeta diagram, bool assignUniqueBase);
        ProjectVariables ensureProjectVariables();
        bool syncVariableSignalAddressesFromDeviceTypes();
        void saveProject();
        string getDefaultUnitId();
        DeviceSignal[] unitSignals { get; }
        DeviceSignal[] projectUnitStructSignals { get; }
    }

    public static CodegenPayload buildCSharpPayload(PayloadContext context, string platform, string? unitId = null)
    {
        if (context.syncVariableSignalAddressesFromDeviceTypes()) context.saveProject();
        return unitId == "__all__"
            ? BuildProjectPayload(context, platform)
            : BuildUnitPayload(context, platform, string.IsNullOrWhiteSpace(unitId) ? context.getDefaultUnitId() : unitId!);
    }

    private static CodegenPayload BuildUnitPayload(PayloadContext context, string platform, string unitId)
    {
        var selectedUnit = unitId == "__none__" ? null : context.project.Units.FirstOrDefault(unit => unit.Id == unitId);
        var diagrams = context.project.Diagrams.Where(diagram => unitId == "__none__" ? string.IsNullOrEmpty(diagram.UnitId) : diagram.UnitId == unitId).ToList();
        var unitInfo = new UnitInfo
        {
            Id = selectedUnit?.Id ?? unitId,
            Name = selectedUnit?.Name ?? (unitId == "__none__" ? "No unit" : string.Empty),
            Label = selectedUnit?.Name ?? (unitId == "__none__" ? "No unit" : string.Empty)
        };
        return BuildPayloadCore(context, platform, unitInfo, diagrams.Select(diagram => BuildFlow(context, diagram)).ToList());
    }

    private static CodegenPayload BuildProjectPayload(PayloadContext context, string platform)
        => BuildPayloadCore(context, platform, null, context.project.Diagrams.Select(diagram => BuildFlow(context, diagram)).ToList());

    private static CodegenPayload BuildPayloadCore(PayloadContext context, string platform, UnitInfo? unit, List<FlowBuildResult> flowResults)
    {
        var assets = context.getAssets();
        var variables = new List<DeviceVariable>();
        var seenVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var flows = flowResults.Select(flowResult =>
        {
            foreach (var variable in flowResult.Variables)
            {
                if (!string.IsNullOrWhiteSpace(variable.Label) && seenVariables.Add(variable.Label)) variables.Add(variable);
            }

            return new FlowInfo
            {
                Id = flowResult.Diagram.Id,
                Name = flowResult.Diagram.Name,
                Type = NormalizeFlowType(flowResult.Diagram.Mode),
                ControlState = flowResult.Diagram.ControlState,
                Category = string.IsNullOrWhiteSpace(flowResult.Diagram.Category) ? "normal" : flowResult.Diagram.Category,
                DiagramType = string.IsNullOrWhiteSpace(flowResult.Diagram.DiagramType) ? "Macro" : flowResult.Diagram.DiagramType,
                OrchestratorConfig = flowResult.Diagram.Category == "orchestrator" ? flowResult.Diagram.OrchestratorConfig ?? new OrchestratorConfig() : null,
                Diagram = flowResult.Diagram,
                Steps = flowResult.Steps,
                Transitions = flowResult.Transitions,
                MacroPortVariable = flowResult.MacroPortVariable
            };
        }).ToList();

        return new CodegenPayload
        {
            Platform = platform,
            DeviceLibraryPath = assets.DeviceLibraryPath,
            TemplateRootPath = assets.TemplateRootPath,
            TemplateProfile = string.IsNullOrWhiteSpace(assets.TemplateProfile) ? "simple" : assets.TemplateProfile,
            Project = new ProjectInfo { Id = context.project.Id, Name = context.project.Name, MachineName = context.project.MachineName },
            Unit = unit,
            Units = BuildUnitsInfo(context.project),
            Flows = flows,
            Variables = variables,
            DeviceTypes = context.project.Devices.ToList()
        };
    }

    private static FlowBuildResult BuildFlow(PayloadContext context, DiagramMeta diagram)
    {
        context.ensureFlowAddressConfig(diagram, true);
        var state = context.loadDiagramData(diagram.Id)?.State ?? new DiagramState();
        var steps = state.Steps.Select(step =>
        {
            var address = ResolveStepAddress(step, diagram);
            return new Step
            {
                Id = step.Id,
                Number = step.Number,
                Label = step.Label,
                IsInitial = step.IsInitial,
                Kind = string.IsNullOrWhiteSpace(step.Kind) ? "normal" : step.Kind,
                MacroFlowId = step.MacroFlowId,
                ExecAddress = address.ExecAddress,
                DoneAddress = address.DoneAddress,
                Actions = step.Actions
            };
        }).ToList();
        var stepIds = steps.Select(step => step.Id).ToHashSet(StringComparer.Ordinal);
        var transitions = state.Transitions.Select(transition => new Transition
        {
            Id = transition.Id,
            Label = transition.Label,
            Condition = transition.Condition,
            FromStepIds = state.Connections.Where(connection => connection.To == transition.Id && stepIds.Contains(connection.From)).Select(connection => connection.From).ToList(),
            ToStepIds = state.Connections.Where(connection => connection.From == transition.Id && stepIds.Contains(connection.To)).Select(connection => connection.To).ToList()
        }).ToList();
        var variables = GetVariables(context, state).ToList();
        var macroPortVariable = string.Equals(diagram.DiagramType, "MacroStep", StringComparison.OrdinalIgnoreCase)
            ? FindMacroPortVariable(diagram.Name, variables)
            : null;

        return new FlowBuildResult
        {
            Diagram = new DiagramInfo
            {
                Id = diagram.Id,
                Name = diagram.Name,
                Mode = diagram.Mode,
                ControlState = string.IsNullOrWhiteSpace(diagram.ControlState) ? "Auto" : diagram.ControlState,
                Category = string.IsNullOrWhiteSpace(diagram.Category) ? "normal" : diagram.Category,
                OrchestratorConfig = diagram.Category == "orchestrator" ? diagram.OrchestratorConfig ?? new OrchestratorConfig() : null,
                UnitId = diagram.UnitId,
                Unit = diagram.Unit,
                DiagramType = string.IsNullOrWhiteSpace(diagram.DiagramType) ? "Macro" : diagram.DiagramType,
                AddressMode = string.IsNullOrWhiteSpace(diagram.AddressMode) ? "bool" : diagram.AddressMode,
                BoolAddressMode = string.IsNullOrWhiteSpace(diagram.BoolAddressMode) ? "linear" : diagram.BoolAddressMode,
                BaseMr = diagram.BaseMr,
                ActiveWord = diagram.ActiveWord,
                CompleteWord = diagram.CompleteWord
            },
            Steps = steps,
            Transitions = transitions,
            Variables = variables,
            MacroPortVariable = macroPortVariable
        };
    }

    private static IEnumerable<DeviceVariable> GetVariables(PayloadContext context, DiagramState state)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var variable in state.Variables.Concat(context.ensureProjectVariables().Imported).Concat(context.ensureProjectVariables().User).Concat(context.project.ExcelVars))
        {
            if (!string.IsNullOrWhiteSpace(variable.Label) && seen.Add(variable.Label)) yield return variable;
        }
    }

    private static DeviceVariable? FindMacroPortVariable(string? flowName, IEnumerable<DeviceVariable> variables)
    {
        var matches = variables.Where(variable => string.Equals(variable.Label?.Trim(), flowName?.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        if (matches.Count == 0) return null;
        if (matches.Count > 1) throw new InvalidOperationException($"Duplicate MacroPort variable name for MacroStep \"{flowName}\".");
        var match = matches[0];
        if (match.Format != "MacroPort") throw new InvalidOperationException($"MacroStep \"{flowName}\" has variable with same name but format/dataType/structure is \"{match.Format}\", expected \"MacroPort\".");
        return match;
    }

    private static (string ExecAddress, string DoneAddress) ResolveStepAddress(Step step, DiagramMeta diagram)
    {
        var baseMr = diagram.BaseMr ?? 0;
        var pairOffset = (Math.Max(1, step.Number) - 1) * 2;
        return ($"@MR{baseMr + pairOffset}", $"@MR{baseMr + pairOffset + 1}");
    }

    private static List<UnitInfo> BuildUnitsInfo(Project project)
    {
        var units = project.Units.Select(unit => new UnitInfo { Id = unit.Id, Name = unit.Name, Label = unit.Name }).ToList();
        if (project.Diagrams.Any(diagram => string.IsNullOrEmpty(diagram.UnitId))) units.Add(new UnitInfo { Id = "__none__", Name = "No unit", Label = "No unit" });
        return units;
    }

    private static string NormalizeFlowType(string? mode)
        => string.Equals(mode, "origin", StringComparison.OrdinalIgnoreCase) ? "origin" : "auto";

    private sealed class FlowBuildResult
    {
        public DiagramInfo Diagram { get; init; } = new();
        public List<Step> Steps { get; init; } = new();
        public List<Transition> Transitions { get; init; } = new();
        public List<DeviceVariable> Variables { get; init; } = new();
        public DeviceVariable? MacroPortVariable { get; init; }
    }
}

public sealed class Project
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string MachineName { get; init; } = string.Empty;
    public List<Unit> Units { get; init; } = new();
    public List<DiagramMeta> Diagrams { get; init; } = new();
    public List<DeviceType> Devices { get; init; } = new();
    public ProjectVariables Variables { get; init; } = new();
    public List<ProjectVariable> ExcelVars { get; init; } = new();
    public Dictionary<string, UnitConfig> UnitConfig { get; init; } = new();
    public IOMapping IOMapping { get; init; } = new();
}

public sealed class Unit
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
}

public sealed class ProjectVariable : DeviceVariable
{
}

public sealed class ProjectVariables
{
    public List<ProjectVariable> Imported { get; init; } = new();
    public List<ProjectVariable> User { get; init; } = new();
}

public sealed class StoredDiagramData
{
    public DiagramState State { get; init; } = new();
    public int NextId { get; init; }
    public int NextStepNum { get; init; }
    public double ViewX { get; init; }
    public double ViewY { get; init; }
    public double ViewScale { get; init; }
}

public sealed class UnitConfig
{
    public string Label { get; init; } = string.Empty;
    public Dictionary<string, string> SignalAddresses { get; init; } = new();
}

public sealed class IOMapping
{
    public List<PhysicalIO> PhysicalIOs { get; init; } = new();
    public List<IOMappingEntry> Entries { get; init; } = new();
}

public sealed class PhysicalIO
{
}

public sealed class IOMappingEntry
{
}

