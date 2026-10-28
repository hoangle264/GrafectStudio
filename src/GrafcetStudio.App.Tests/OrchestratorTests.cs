using GrafcetStudio.App.Generators;
using GrafcetStudio.App.Generators.Common;
using GrafcetStudio.App.Generators.Siemens;
using GrafcetStudio.CodeGen.Template;
using GrafcetStudio.Domain.Models;
using GrafcetStudio.Domain.Resolution;
using HandlebarsDotNet;
using Xunit;
using Xunit.Abstractions;
using System.Text.Json;

namespace GrafcetStudio.App.Tests;

public class OrchestratorTests
{
    private const string UnitId = "unit-1";
    private readonly ITestOutputHelper _output;

    public OrchestratorTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Build_SequenceThenParallel_ResolvesStagesAndPorts()
    {
        var payload = BuildPayload(
            Transition("t1", ["s1"], ["s2", "s3"]),
            Transition("t2", ["s2", "s3"], ["s1"]));

        var orchestrator = Assert.Single(BuildContexts(payload));

        Assert.Equal("Orch", orchestrator.name);
        Assert.Equal(UnitId, orchestrator.unit.id);
        Assert.Equal("Main", orchestrator.unit.label);
        Assert.True(orchestrator.loopsToStart);
        Assert.Collection(orchestrator.stages,
            stage =>
            {
                Assert.True(stage.isFirst);
                Assert.False(stage.isParallel);
                var macro = Assert.Single(stage.macros);
                Assert.Equal("MS_A", macro.macroStep);
                Assert.Equal("@MR100", macro.execAddress);
                Assert.Equal("@MR101", macro.doneAddress);
                Assert.Equal("MS_A", macro.port.label);
                Assert.Equal("@MR500", macro.port.signals["Enable"]);
                Assert.Equal("@MR503", macro.port.signals["Done"]);
                Assert.Equal(["@MR102", "@MR104"], stage.nextMacros.Select(macro => macro.execAddress));
            },
            stage =>
            {
                Assert.True(stage.isLast);
                Assert.True(stage.isParallel);
                Assert.Equal(["MS_B", "MS_C"], stage.macros.Select(macro => macro.macroStep));
                Assert.Equal(["@MR100"], stage.nextMacros.Select(macro => macro.execAddress));
            });
    }

    [Fact]
    public void Build_SequenceWithoutReturn_EndsAfterLastStage()
    {
        var payload = BuildPayload(
            Transition("t1", ["s1"], ["s2"]),
            Transition("t2", ["s2"], ["s3"]));

        var orchestrator = Assert.Single(BuildContexts(payload));

        Assert.False(orchestrator.loopsToStart);
        Assert.Equal(3, orchestrator.stages.Count);
        Assert.Empty(orchestrator.stages[2].nextMacros);
    }

    [Fact]
    public void Build_MacroStepWithoutPort_Throws()
    {
        var payload = BuildPayload(Transition("t1", ["s1"], ["s2", "s3"]), Transition("t2", ["s2", "s3"], ["s1"]));
        payload.Variables.RemoveAll(variable => variable.Label == "MS_B");

        var error = Assert.Throws<InvalidOperationException>(() => BuildContexts(payload));
        Assert.Contains("MacroStep MS_B has no MacroPort variable", error.Message);
    }

    [Fact]
    public void Build_PortWithWrongFormat_Throws()
    {
        var payload = BuildPayload(Transition("t1", ["s1"], ["s2", "s3"]), Transition("t2", ["s2", "s3"], ["s1"]));
        payload.Variables.RemoveAll(variable => variable.Label == "MS_B");
        payload.Variables.Add(new DeviceVariable { Label = "MS_B", Format = "Motor" });

        var error = Assert.Throws<InvalidOperationException>(() => new SystemControlGenerator().GenerateOrchestrator(payload));
        Assert.Contains("expected MacroPort", error.Message);
    }

    [Fact]
    public void Build_NormalStep_Throws()
    {
        var payload = BuildPayload(Transition("t1", ["s1"], ["s2", "s3"]), Transition("t2", ["s2", "s3"], ["s1"]));
        var orchestrator = payload.Flows.Single(flow => flow.Id == "orch");
        orchestrator.Steps[1] = new Step { Id = "s2", Number = 2, Label = "S2", Kind = "normal", ExecAddress = "@MR102" };

        var error = Assert.Throws<InvalidOperationException>(() => BuildContexts(payload));
        Assert.Contains("step S2 is not a macro step", error.Message);
    }

    [Fact]
    public void Build_NoInitialStep_Throws()
    {
        var payload = BuildPayload(Transition("t1", ["s1"], ["s2", "s3"]), Transition("t2", ["s2", "s3"], ["s1"]));
        var orchestrator = payload.Flows.Single(flow => flow.Id == "orch");
        orchestrator.Steps[0] = MacroStep("s1", 1, "MS_A", "@MR100", initial: false);

        var error = Assert.Throws<InvalidOperationException>(() => BuildContexts(payload));
        Assert.Contains("exactly one initial step, found 0", error.Message);
    }

    [Fact]
    public void Build_OrBranch_Throws()
    {
        var payload = BuildPayload(
            Transition("t1", ["s1"], ["s2"]),
            Transition("t2", ["s1"], ["s3"]));

        var error = Assert.Throws<InvalidOperationException>(() => BuildContexts(payload));
        Assert.Contains("OR branches are not supported", error.Message);
    }

    [Fact]
    public void Build_NestedParallelBranch_Throws()
    {
        var payload = BuildPayload(
            Transition("t1", ["s1"], ["s2", "s3"]),
            Transition("t2", ["s2"], ["s4"]),
            Transition("t3", ["s3", "s4"], ["s1"]));
        AddMacroStep(payload, "s4", 4, "MS_D", "@MR106", "@MR520");

        var error = Assert.Throws<InvalidOperationException>(() => BuildContexts(payload));
        Assert.Contains("Nested or partial parallel branches are not supported", error.Message);
    }

    [Fact]
    public void Build_JumpBackToNonInitialStep_Throws()
    {
        var payload = BuildPayload(
            Transition("t1", ["s1"], ["s2"]),
            Transition("t2", ["s2"], ["s3"]),
            Transition("t3", ["s3"], ["s2"]));

        var error = Assert.Throws<InvalidOperationException>(() => BuildContexts(payload));
        Assert.Contains("step S2", error.Message);
    }

    [Fact]
    public void GeneratorContext_KeepsOrchestratorOutOfUnitFlows()
    {
        var payload = BuildPayload(Transition("t1", ["s1"], ["s2", "s3"]), Transition("t2", ["s2", "s3"], ["s1"]));

        var context = GeneratorContextBuilder.Build(payload, new SequenceResolver());

        Assert.DoesNotContain(context.autoFlows, flow => flow.id == "orch");
        Assert.DoesNotContain(context.macroFlows, flow => flow.id == "orch");
        Assert.Contains(context.flows, flow => flow.id == "orch" && flow.isOrchestrator);
        Assert.Equal(3, context.macroStepFlows.Count);
        Assert.Single(context.orchestrators);
    }

    [Fact]
    public void GenerateOrchestrator_EmitsOrchestratorsModel()
    {
        var payload = BuildPayload(Transition("t1", ["s1"], ["s2", "s3"]), Transition("t2", ["s2", "s3"], ["s1"]));

        var output = new SystemControlGenerator().GenerateOrchestrator(payload);

        Assert.Contains("\"orchestrators\"", output);
        Assert.Contains("\"stages\"", output);
        Assert.DoesNotContain("skeleton", output);
    }

    [Theory]
    [InlineData("simple")]
    [InlineData("packml")]
    public void KeyenceTemplate_RendersOrchestratorMnemonic(string profile)
    {
        if (!LocalTemplateExists("simple", "Orchestrator.hbs")) return;

        var templates = new TemplateManager(Handlebars.Create());
        var loaded = TemplateLoader.LoadFromPath(Path.Combine(AppContext.BaseDirectory, "templates"));
        templates.RegisterTemplates(loaded.Templates.Values.ToList());
        var payload = BuildPayload(Transition("t1", ["s1"], ["s2", "s3"]), Transition("t2", ["s2", "s3"], ["s1"]));
        payload.TemplateProfile = profile;
        var service = new CodeGeneratorService([new OrchestratorOnlyGenerator()], templates);

        var content = Assert.Single(service.Generate("Keyence", payload).Files).Content;
        _output.WriteLine(content);
        var lines = content.Split(["\r\n", "\n"], StringSplitOptions.None).Select(line => string.Join(" ", line.Split(" ", StringSplitOptions.RemoveEmptyEntries))).ToList();

        Assert.DoesNotContain("->", content);
        Assert.Contains(";<h1/> Orchestrator Orch", lines);
        // Initial step on the first scan.
        AssertSequence(lines, "LD CR2008", "SET @MR100");
        // Enable is latched on the first scan of the step: Skipped when Enable is off, then Locked.
        AssertSequence(lines, "LD @MR100", "ANB @MR505", "ANB @MR500", "SET @MR504");
        AssertSequence(lines, "LD @MR100", "SET @MR505");
        // Start only while not skipped; the step is done on Done or Skipped.
        AssertSequence(lines, "LD @MR100", "ANB @MR504", "OUT @MR501");
        AssertSequence(lines, "LD @MR100", "LD @MR503", "OR @MR504", "ANL", "OUT @MR101");
        // Parallel stage waits for both macros, then returns to the initial step.
        AssertSequence(lines, "LD @MR103", "AND @MR105", "RST @MR102");
        AssertSequence(lines, "LD @MR103", "AND @MR105", "RST @MR505");
        AssertSequence(lines, "LD @MR103", "AND @MR105", "SET @MR100");
    }

    private static void AssertSequence(IList<string> lines, params string[] expected)
    {
        for (var start = 0; start + expected.Length <= lines.Count; start++)
        {
            if (expected.Select((line, offset) => lines[start + offset] == line).All(match => match)) return;
        }

        Assert.Fail($"Sequence not found: {string.Join(" / ", expected)}");
    }

    private sealed class OrchestratorOnlyGenerator : ICodeGenerator
    {
        public string Platform => "Keyence";

        public IEnumerable<CodegenFile> GenerateFiles(CodegenPayload payload)
        {
            yield return new CodegenFile { Path = "Orchestrator.mnm", Content = new SystemControlGenerator().GenerateOrchestrator(payload) };
        }
    }

    [Fact]
    public void SiemensLadTemplate_GeneratesOrchestratorNetworks()
    {
        if (!LocalTemplateExists("siemens-lad", "default.lad.hbs")) return;

        var payload = WithSiemensAddresses(BuildPayload(Transition("t1", ["s1"], ["s2", "s3"]), Transition("t2", ["s2", "s3"], ["s1"])));

        var file = Assert.Single(new SiemensLadDslGenerator(new SequenceResolver()).GenerateFiles(payload));
        _output.WriteLine(file.Content);

        Assert.Contains("FirstScan", file.Content);
        // Locked of MS_A and the orchestrator step bits are driven; the orchestrator itself is not a flow network.
        Assert.Contains("Tag_505", file.Content);
        Assert.Contains("Tag_105", file.Content);
        Assert.DoesNotContain("Complete S1", file.Content);
        // 1 initial + 4 per macro + per stage (leave + 3 per next macro): 1 + 12 + (1 + 6) + (2 + 3), plus 3 MacroStep body steps.
        Assert.Equal(28, CountOccurrences(file.Content, "SW.Blocks.CompileUnit "));
    }

    /// <summary>
    /// templates/** is not in git, so these tests only run where the local templates were copied to the output.
    /// </summary>
    private bool LocalTemplateExists(params string[] path)
    {
        var file = Path.Combine([AppContext.BaseDirectory, "templates", .. path]);
        if (File.Exists(file)) return true;

        _output.WriteLine($"Skipped: local template {file} not found (templates are not tracked in git).");
        return false;
    }

    private static int CountOccurrences(string text, string value)
        => (text.Length - text.Replace(value, string.Empty).Length) / value.Length;

    /// <summary>Siemens tags cannot contain '@', so swap the Keyence-style fixture addresses for symbolic tags.</summary>
    private static CodegenPayload WithSiemensAddresses(CodegenPayload payload)
        => JsonSerializer.Deserialize<CodegenPayload>(JsonSerializer.Serialize(payload).Replace("@MR", "Tag_"))!;

    private static IList<OrchestratorContext> BuildContexts(CodegenPayload payload)
        => OrchestratorContextBuilder.Build(payload.Flows, payload.Variables, payload.DeviceTypes, payload.Units);

    /// <summary>Orchestrator s1 (MS_A, initial), s2 (MS_B), s3 (MS_C) wired by the given transitions.</summary>
    internal static CodegenPayload BuildPayload(params Transition[] transitions)
    {
        var payload = new CodegenPayload
        {
            Project = new ProjectInfo { Name = "Demo" },
            Units = [new UnitInfo { Id = UnitId, Name = "Main", Label = "Main" }],
            DeviceTypes =
            [
                new DeviceType
                {
                    Name = FlowConstants.MacroPortFormat,
                    Signals =
                    [
                        new DeviceSignal { Id = "sig-enable", Name = "Enable" },
                        new DeviceSignal { Id = "sig-start", Name = "Start" },
                        new DeviceSignal { Id = "sig-busy", Name = "Busy" },
                        new DeviceSignal { Id = "sig-done", Name = "Done" },
                        new DeviceSignal { Id = "sig-skipped", Name = "Skipped" },
                        new DeviceSignal { Id = "sig-locked", Name = "Locked" }
                    ]
                }
            ],
            Flows =
            [
                new FlowInfo
                {
                    Id = "orch",
                    Name = "Orch",
                    Type = "auto",
                    Category = FlowConstants.OrchestratorCategory,
                    ControlState = "Auto",
                    Diagram = new DiagramInfo { Id = "orch", Name = "Orch", UnitId = UnitId, Category = FlowConstants.OrchestratorCategory },
                    Steps =
                    [
                        MacroStep("s1", 1, "MS_A", "@MR100", initial: true),
                        MacroStep("s2", 2, "MS_B", "@MR102"),
                        MacroStep("s3", 3, "MS_C", "@MR104")
                    ],
                    Transitions = transitions.ToList()
                }
            ]
        };

        AddMacroStepFlow(payload, "MS_A", "@MR500");
        AddMacroStepFlow(payload, "MS_B", "@MR510");
        AddMacroStepFlow(payload, "MS_C", "@MR530");
        return payload;
    }

    private static void AddMacroStep(CodegenPayload payload, string stepId, int number, string macroStep, string execAddress, string portBase)
    {
        payload.Flows.Single(flow => flow.Id == "orch").Steps.Add(MacroStep(stepId, number, macroStep, execAddress));
        AddMacroStepFlow(payload, macroStep, portBase);
    }

    private static void AddMacroStepFlow(CodegenPayload payload, string name, string portBase)
    {
        var number = int.Parse(portBase["@MR".Length..]);
        payload.Flows.Add(new FlowInfo
        {
            Id = name,
            Name = name,
            DiagramType = FlowConstants.MacroStepDiagramType,
            Diagram = new DiagramInfo { Id = name, Name = name, UnitId = UnitId, DiagramType = FlowConstants.MacroStepDiagramType },
            Steps = [new Step { Id = name + "-s1", Number = 1, Label = name + "_1", IsInitial = true, ExecAddress = $"@MR{number + 10}", DoneAddress = $"@MR{number + 11}" }]
        });
        payload.Variables.Add(new DeviceVariable
        {
            Label = name,
            Format = FlowConstants.MacroPortFormat,
            SignalAddresses = new Dictionary<string, string>
            {
                ["sig-enable"] = $"@MR{number}",
                ["sig-start"] = $"@MR{number + 1}",
                ["sig-busy"] = $"@MR{number + 2}",
                ["sig-done"] = $"@MR{number + 3}",
                ["sig-skipped"] = $"@MR{number + 4}",
                ["sig-locked"] = $"@MR{number + 5}"
            }
        });
    }

    private static Step MacroStep(string id, int number, string macroFlowId, string execAddress, bool initial = false)
    {
        var mr = int.Parse(execAddress["@MR".Length..]);
        return new Step
        {
            Id = id,
            Number = number,
            Label = "S" + number,
            Kind = FlowConstants.MacroStepKind,
            MacroFlowId = macroFlowId,
            IsInitial = initial,
            ExecAddress = execAddress,
            DoneAddress = $"@MR{mr + 1}"
        };
    }

    private static Transition Transition(string id, string[] from, string[] to)
        => new() { Id = id, Label = id.ToUpperInvariant(), FromStepIds = from, ToStepIds = to };
}
