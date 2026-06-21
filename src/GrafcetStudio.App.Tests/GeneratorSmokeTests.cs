using GrafcetStudio.App.Generators;
using GrafcetStudio.CodeGen.Runtime.Models;
using GrafcetStudio.Domain.Models;
using Xunit;

namespace GrafcetStudio.App.Tests;

public class GeneratorSmokeTests
{
    [Fact]
    public void MapIOGenerator_ReturnsExpectedSections()
    {
        var generator = new MapIOGenerator();
        var payload = BuildPayload();
        var output = generator.Generate(payload, Array.Empty<AggregatedOutputBinding>());
        Assert.Contains("deviceOutputGroups", output);
        Assert.Contains("unit", output);
    }

    [Fact]
    public void ErrorGenerator_ReturnsErrorsArray()
    {
        var generator = new ErrorGenerator();
        var payload = BuildPayload();
        var output = generator.Generate(payload);
        Assert.Contains("\"errors\"", output);
    }

    [Fact]
    public void DeviceManagerGenerator_ReturnsDeviceTypes()
    {
        var generator = new DeviceManagerGenerator();
        var payload = BuildPayload();
        var output = generator.Generate(payload);
        Assert.Contains("deviceTypes", output);
        Assert.Contains("DeviceManager_", output);
    }

    [Fact]
    public void SystemControlGenerator_ReturnsSkeleton()
    {
        var generator = new SystemControlGenerator();
        var payload = BuildPayload();
        payload.Flows.Add(new FlowInfo
        {
            Id = "orch-1",
            Name = "Orchestrator",
            Category = "orchestrator",
            ControlState = "Auto",
            OrchestratorConfig = new OrchestratorConfig
            {
                Elements = new List<OrchestratorElement>
                {
                    new() { Type = "noop" }
                }
            }
        });

        var output = generator.Generate(payload);
        Assert.Contains("SystemControl.st", output);
        Assert.Contains("orchestratorFlows", output);
        Assert.Contains("skeleton", output);
    }

    [Fact]
    public void BuildCSharpPayload_KeepsLegacyControlStateAsAuto()
    {
        var context = new TestPayloadContext(BuildProject(new[]
        {
            new DiagramMeta { Id = "diag-legacy", Name = "Legacy", Mode = "Main", UnitId = "unit-1" }
        }));

        var payload = GrafcetStudioCodegenPayload.buildCSharpPayload(context, "twincat-st", "unit-1");

        Assert.Single(payload.Flows);
        Assert.Equal("Auto", payload.Flows[0].ControlState);
    }

    [Fact]
    public void BuildCSharpPayload_PreservesMultipleUnitFilesAndControlStates()
    {
        var context = new TestPayloadContext(BuildProject(new[]
        {
            new DiagramMeta { Id = "diag-a", Name = "Unit A", Mode = "Main", ControlState = "Auto", UnitId = "unit-a" },
            new DiagramMeta { Id = "diag-b", Name = "Unit B", Mode = "Manual", ControlState = "Manual", UnitId = "unit-b" }
        }, new[]
        {
            new Unit { Id = "unit-a", Name = "Unit A" },
            new Unit { Id = "unit-b", Name = "Unit B" }
        }));

        var payloadA = GrafcetStudioCodegenPayload.buildCSharpPayload(context, "twincat-st", "unit-a");
        var payloadB = GrafcetStudioCodegenPayload.buildCSharpPayload(context, "twincat-st", "unit-b");

        Assert.Single(payloadA.Flows);
        Assert.Single(payloadB.Flows);
        Assert.Equal("Auto", payloadA.Flows[0].ControlState);
        Assert.Equal("Manual", payloadB.Flows[0].ControlState);
    }

    [Fact]
    public void BuildCSharpPayload_OrchestratorFlowKeepsSystemControlCategory()
    {
        var context = new TestPayloadContext(BuildProject(new[]
        {
            new DiagramMeta
            {
                Id = "diag-orch",
                Name = "System Control",
                Category = "orchestrator",
                ControlState = "Auto",
                UnitId = "unit-1",
                OrchestratorConfig = new OrchestratorConfig
                {
                    Elements = new List<OrchestratorElement> { new() { Type = "noop" } }
                }
            }
        }));

        var payload = GrafcetStudioCodegenPayload.buildCSharpPayload(context, "twincat-st", "unit-1");

        Assert.Single(payload.Flows);
        Assert.Equal("orchestrator", payload.Flows[0].Category);
        Assert.NotNull(payload.Flows[0].OrchestratorConfig);
    }

    [Fact]
    public void BuildCSharpPayload_ResolvesAllDeviceTypes()
    {
        var context = new TestPayloadContext(BuildProject(Array.Empty<DiagramMeta>(), new[]
        {
            new DeviceType { Name = "Motor", Signals = new List<DeviceSignal> { new() { Id = "run", Name = "Run" } } },
            new DeviceType { Name = "Cylinder", Signals = new List<DeviceSignal> { new() { Id = "state", Name = "State" } } }
        }))
        {
            ProjectVariables = new ProjectVariables
            {
                Imported = new List<ProjectVariable>
                {
                    new() { Label = "M1", Format = "Motor", SignalAddresses = new Dictionary<string, string> { ["Run"] = "Q0.0" } },
                    new() { Label = "C1", Format = "Cylinder", SignalAddresses = new Dictionary<string, string> { ["State"] = "M100" } }
                },
                User = new List<ProjectVariable>()
            }
        };

        var payload = GrafcetStudioCodegenPayload.buildCSharpPayload(context, "twincat-st", "unit-1");

        Assert.Equal(2, payload.DeviceTypes.Count);
    }

    private static CodegenPayload BuildPayload()
        => new()
        {
            Project = new ProjectInfo { Name = "Demo" },
            Unit = new UnitInfo { Name = "Main" },
            Variables = new List<DeviceVariable>
            {
                new() { Label = "UnitMain", Format = "Motor", Address = "@M1" }
            },
            DeviceTypes = new List<DeviceType>
            {
                new() { Name = "Motor", Signals = new List<DeviceSignal> { new() { Id = "run", Name = "Run" } } }
            },
            Flows = new List<FlowInfo>
            {
                new() { Transitions = new List<Transition> { new() { Id = "t1", Label = "T1" } } }
            }
        };

    private static Project BuildProject(IEnumerable<DiagramMeta> diagrams, IEnumerable<Unit>? units = null, IEnumerable<DeviceType>? deviceTypes = null)
        => new()
        {
            Id = "project-1",
            Name = "Demo",
            MachineName = "Machine",
            Units = (units ?? new[] { new Unit { Id = "unit-1", Name = "Main" } }).ToList(),
            Diagrams = diagrams.ToList(),
            Devices = (deviceTypes ?? new[]
            {
                new DeviceType { Name = "Motor", Signals = new List<DeviceSignal> { new() { Id = "run", Name = "Run" } } }
            }).ToList(),
            Variables = new ProjectVariables { Imported = new List<ProjectVariable>(), User = new List<ProjectVariable>() },
            ExcelVars = new List<ProjectVariable>(),
            UnitConfig = new Dictionary<string, UnitConfig>(),
            IOMapping = new IOMapping { PhysicalIOs = new List<PhysicalIO>(), Entries = new List<IOMappingEntry>() }
        };

    private sealed class TestPayloadContext : GrafcetStudioCodegenPayload.PayloadContext
    {
        public TestPayloadContext(Project project)
        {
            Project = project;
        }

        public Project Project { get; }
        public ProjectVariables? ProjectVariables { get; set; }

        public CodegenAssets GetAssets() => new()
        {
            DeviceLibraryPath = "config/Devices.json",
            TemplateRootPath = "templates",
            OutputPath = "out",
            TemplateProfile = "simple"
        };

        public StoredDiagramData? LoadDiagramData(string diagramId) => new()
        {
            State = new DiagramState
            {
                Steps = new List<Step> { new() { Id = diagramId + "-s1", Number = 1 } },
                Transitions = new List<Transition>(),
                Connections = new List<Connection>(),
                Variables = new List<DeviceVariable>()
            },
            NextId = 1,
            NextStepNum = 2,
            ViewX = 0,
            ViewY = 0,
            ViewScale = 1
        };

        public bool SyncVariableSignalAddressesFromDeviceTypes() => false;

        public ProjectVariables EnsureProjectVariables() => ProjectVariables ??= new ProjectVariables { Imported = new List<ProjectVariable>(), User = new List<ProjectVariable>() };

        public void SaveProject() { }

        public string GetDefaultUnitId() => "unit-1";

        public DeviceSignal[] UnitSignals => Array.Empty<DeviceSignal>();

        public DeviceSignal[] ProjectUnitStructSignals => Array.Empty<DeviceSignal>();

        Project GrafcetStudioCodegenPayload.PayloadContext.project => Project;
        GrafcetStudioProject.StoredDiagramData? GrafcetStudioCodegenPayload.PayloadContext.loadDiagramData(string diagramId) => LoadDiagramData(diagramId);
        GrafcetStudioCodegenPayload.CodegenAssets GrafcetStudioCodegenPayload.PayloadContext.getAssets() => GetAssets();
        object? GrafcetStudioCodegenPayload.PayloadContext.ensureFlowAddressConfig(GrafcetStudioProject.DiagramMeta diagram, bool assignUniqueBase) => null;
        GrafcetStudioProject.ProjectVariables GrafcetStudioCodegenPayload.PayloadContext.ensureProjectVariables() => EnsureProjectVariables();
        bool GrafcetStudioCodegenPayload.PayloadContext.syncVariableSignalAddressesFromDeviceTypes() => SyncVariableSignalAddressesFromDeviceTypes();
        void GrafcetStudioCodegenPayload.PayloadContext.saveProject() => SaveProject();
        string GrafcetStudioCodegenPayload.PayloadContext.getDefaultUnitId() => GetDefaultUnitId();
        GrafcetStudioProject.DeviceSignal[] GrafcetStudioCodegenPayload.PayloadContext.unitSignals => UnitSignals;
        GrafcetStudioProject.DeviceSignal[] GrafcetStudioCodegenPayload.PayloadContext.projectUnitStructSignals => ProjectUnitStructSignals;
    }
}
