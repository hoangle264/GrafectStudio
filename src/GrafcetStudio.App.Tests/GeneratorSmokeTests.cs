using GrafcetStudio.App.Generators;
using GrafcetStudio.CodeGen.Runtime.Models;
using GrafcetStudio.CodeGen.Template;
using GrafcetStudio.App.Generators.Siemens;
using GrafcetStudio.Domain.Resolution;
using HandlebarsDotNet;
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
    public void SiemensLadDslGenerator_CustomTemplateRootOverridesDefaultTemplate()
    {
        var templateRoot = Path.Combine(Path.GetTempPath(), "grafcetstudio-siemens-lad-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(templateRoot);
        File.WriteAllText(Path.Combine(templateRoot, "default.lad.json"), """
        {
          "version": "1.0",
          "platform": "siemens-lad",
          "tiaVersion": 17,
          "blockName": "DefaultTemplate_Should_Not_Be_Used",
          "blockNumber": 99,
          "parameters": [
            { "key": "exec", "source": "step.execAddress", "default": "Exec", "scope": "global", "dataType": "Bool" }
          ],
          "networks": [
            {
              "id": "default",
              "title": "DefaultTemplateNetwork",
              "comment": "Default fallback template",
              "expression": { "type": "TAG", "ref": "exec" },
              "output": { "type": "coil", "ref": "exec" }
            }
          ]
        }
        """);
        File.WriteAllText(Path.Combine(templateRoot, "siemens-lad.json"), """
        {
          "version": "1.0",
          "platform": "siemens-lad",
          "tiaVersion": 17,
          "blockName": "{{unit.name}}_CustomOverride_LAD",
          "blockNumber": 10,
          "parameters": [
            { "key": "prevDone", "source": "step.doneAddress", "default": "PrevDone", "scope": "global", "dataType": "Bool" },
            { "key": "condition", "source": "transition.condition", "default": "Condition", "scope": "global", "dataType": "Bool" },
            { "key": "exec", "source": "step.execAddress", "default": "Exec", "scope": "global", "dataType": "Bool" }
          ],
          "networks": [
            {
              "id": "activation",
              "title": "CustomOverrideActivation",
              "comment": "custom root siemens-lad.json override",
              "expression": {
                "type": "AND",
                "nodes": [
                  { "type": "TAG", "ref": "prevDone" },
                  { "type": "TAG", "ref": "condition" }
                ]
              },
              "output": { "type": "coil", "ref": "exec" }
            }
          ]
        }
        """);

        try
        {
            var payload = BuildPayload();
            payload.TemplateRootPath = templateRoot;
            payload.Unit = new UnitInfo { Id = "unit-1", Name = "Main Unit", Label = "MainUnit" };
            payload.Flows = new List<FlowInfo>
            {
                new()
                {
                    Id = "flow-1",
                    Diagram = new DiagramInfo { Id = "diag-1", UnitId = "unit-1", Unit = "MainUnit" },
                    Steps = new List<Step>
                    {
                        new() { Id = "s1", Number = 1, ExecAddress = "M0.0", DoneAddress = "M0.1" }
                    },
                    Transitions = new List<Transition>
                    {
                        new() { Id = "t1", Condition = "M0.2" }
                    }
                }
            };

            var file = Assert.Single(new SiemensLadDslGenerator().GenerateFiles(payload));

            Assert.Equal("MainUnit_CustomOverride_LAD.xml", file.Path);
            Assert.Contains("SW.Blocks.FC", file.Content);
            Assert.Contains("FlgNet", file.Content);
            Assert.Contains("Name=\"Contact\"", file.Content);
            Assert.Contains("Name=\"Coil\"", file.Content);
            Assert.Contains("CustomOverrideActivation", file.Content);
            Assert.DoesNotContain("DefaultTemplate_Should_Not_Be_Used", file.Content);
            Assert.DoesNotContain("DefaultTemplateNetwork", file.Content);
        }
        finally
        {
            if (Directory.Exists(templateRoot)) Directory.Delete(templateRoot, true);
        }
    }
    [Fact]
    public void SiemensLadDslGenerator_RepeatNetworksUseCurrentGrafcetContext()
    {
        var templateRoot = Path.Combine(Path.GetTempPath(), "grafcetstudio-siemens-lad-repeat-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(templateRoot);
        File.WriteAllText(Path.Combine(templateRoot, "siemens-lad.json"), """
        {
          "version": "1.0",
          "platform": "siemens-lad",
          "tiaVersion": 17,
          "blockName": "{{unit.name}}_Repeat_LAD",
          "blockNumber": 11,
          "parameters": [
            { "key": "exec", "source": "step.execAddress", "default": "Exec", "scope": "global", "dataType": "Bool" },
            { "key": "done", "source": "step.doneAddress", "default": "Done", "scope": "global", "dataType": "Bool" },
            { "key": "actionAddr", "source": "action.address", "default": "Action", "scope": "global", "dataType": "Bool" },
            { "key": "condition", "source": "transition.condition", "default": "Condition", "scope": "global", "dataType": "Bool" }
          ],
          "networks": [
            {
              "id": "step_done",
              "repeat": "steps",
              "title": "Step {{flow.name}} {{step.number}} {{step.label}}",
              "comment": "Done for {{step.label}}",
              "expression": { "type": "TAG", "ref": "exec" },
              "output": { "type": "coil", "ref": "done" }
            },
            {
              "id": "transition",
              "repeat": "transitions",
              "title": "Transition {{transition.label}} in {{flow.name}}",
              "comment": "Condition {{transition.label}}",
              "expression": { "type": "TAG", "ref": "condition" },
              "output": { "type": "coil", "ref": "condition" }
            },
            {
              "id": "actions",
              "repeat": "actions",
              "title": "Action {{action.variable}} on {{step.label}}",
              "comment": "Action variable {{action.variable}}",
              "expression": { "type": "TAG", "ref": "exec" },
              "output": { "type": "coil", "ref": "actionAddr" }
            }
          ]
        }
        """);

        try
        {
            var payload = BuildPayload();
            payload.TemplateRootPath = templateRoot;
            payload.Unit = new UnitInfo { Id = "unit-1", Name = "Main Unit", Label = "MainUnit" };
            payload.Flows = new List<FlowInfo>
            {
                new()
                {
                    Id = "flow-1",
                    Name = "MainFlow",
                    Diagram = new DiagramInfo { Id = "diag-1", Name = "MainFlow", UnitId = "unit-1", Unit = "MainUnit" },
                    Steps = new List<Step>
                    {
                        new()
                        {
                            Id = "s1",
                            Number = 1,
                            Label = "Start",
                            ExecAddress = "M0.0",
                            DoneAddress = "M0.1",
                            Actions = new List<StepAction> { new() { Variable = "ValveA", Address = "Q0.0" } }
                        },
                        new()
                        {
                            Id = "s2",
                            Number = 2,
                            Label = "Clamp",
                            ExecAddress = "M0.2",
                            DoneAddress = "M0.3",
                            Actions = new List<StepAction> { new() { Variable = "ValveB", Address = "Q0.1" } }
                        }
                    },
                    Transitions = new List<Transition>
                    {
                        new() { Id = "t1", Label = "T1", Condition = "I0.0" },
                        new() { Id = "t2", Label = "T2", Condition = "I0.1" }
                    }
                }
            };

            var file = Assert.Single(new SiemensLadDslGenerator().GenerateFiles(payload));

            Assert.Equal("MainUnit_Repeat_LAD.xml", file.Path);
            Assert.Equal(6, CountOccurrences(file.Content, "<SW.Blocks.CompileUnit"));
            Assert.Contains("Step MainFlow 1 Start", file.Content);
            Assert.Contains("Step MainFlow 2 Clamp", file.Content);
            Assert.Contains("Transition T1 in MainFlow", file.Content);
            Assert.Contains("Transition T2 in MainFlow", file.Content);
            Assert.Contains("Action ValveA on Start", file.Content);
            Assert.Contains("Action ValveB on Clamp", file.Content);
        }
        finally
        {
            if (Directory.Exists(templateRoot)) Directory.Delete(templateRoot, true);
        }
    }
    [Fact]
    public void SiemensLadDslGenerator_InvalidTemplates_ReturnHelpfulValidationErrors()
    {
        var cases = new[]
        {
            (
                Json: """
                {
                  "platform": "wrong-platform",
                  "parameters": [{ "key": "exec" }],
                  "networks": [{ "id": "n1", "expression": { "type": "TAG", "ref": "exec" }, "output": { "ref": "exec" } }]
                }
                """,
                Expected: new[] { "siemens-lad.json", "JSON path 'platform'", "must be 'siemens-lad'" }
            ),
            (
                Json: """
                {
                  "platform": "siemens-lad",
                  "parameters": [{ "key": "exec" }, { "key": "exec" }],
                  "networks": [{ "id": "n1", "expression": { "type": "TAG", "ref": "exec" }, "output": { "ref": "exec" } }]
                }
                """,
                Expected: new[] { "siemens-lad.json", "JSON path 'parameters[1].key'", "duplicate parameter key 'exec'" }
            ),
            (
                Json: """
                {
                  "platform": "siemens-lad",
                  "parameters": [{ "key": "exec" }],
                  "networks": [{ "id": "", "expression": { "type": "TAG", "ref": "exec" }, "output": { "ref": "exec" } }]
                }
                """,
                Expected: new[] { "siemens-lad.json", "JSON path 'networks[0].id'", "must not be empty" }
            ),
            (
                Json: """
                {
                  "platform": "siemens-lad",
                  "parameters": [{ "key": "exec" }],
                  "networks": [{ "id": "n1", "output": { "ref": "exec" } }]
                }
                """,
                Expected: new[] { "siemens-lad.json", "Network 'n1'", "JSON path 'networks[0].expression'", "must not be null" }
            ),
            (
                Json: """
                {
                  "platform": "siemens-lad",
                  "parameters": [{ "key": "exec" }],
                  "networks": [{ "id": "n1", "expression": { "type": "TAG", "ref": "exec" }, "output": { "ref": "missing" } }]
                }
                """,
                Expected: new[] { "siemens-lad.json", "Network 'n1'", "JSON path 'networks[0].output.ref'", "unknown parameter 'missing'" }
            ),
            (
                Json: """
                {
                  "platform": "siemens-lad",
                  "parameters": [{ "key": "exec" }],
                  "networks": [{ "id": "n1", "expression": { "type": "TAG", "ref": "missing" }, "output": { "ref": "exec" } }]
                }
                """,
                Expected: new[] { "siemens-lad.json", "Network 'n1'", "JSON path 'networks[0].expression.ref'", "TAG references unknown parameter 'missing'" }
            ),
            (
                Json: """
                {
                  "platform": "siemens-lad",
                  "parameters": [{ "key": "exec" }],
                  "networks": [{ "id": "n1", "expression": { "type": "AND", "nodes": [] }, "output": { "ref": "exec" } }]
                }
                """,
                Expected: new[] { "siemens-lad.json", "Network 'n1'", "JSON path 'networks[0].expression.nodes'", "AND expression must have at least one node" }
            ),
            (
                Json: """
                {
                  "platform": "siemens-lad",
                  "parameters": [{ "key": "exec" }],
                  "networks": [{ "id": "n1", "expression": { "type": "NOT", "node": { "type": "AND", "nodes": [{ "type": "TAG", "ref": "exec" }] } }, "output": { "ref": "exec" } }]
                }
                """,
                Expected: new[] { "siemens-lad.json", "Network 'n1'", "JSON path 'networks[0].expression.node.type'", "NOT currently supports only TAG nodes" }
            )
        };

        foreach (var testCase in cases)
        {
            AssertInvalidSiemensTemplate(testCase.Json, testCase.Expected);
        }
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
    public void BuildCSharpPayload_AllUnits_IncludesUnitsAndFlowsAcrossProject()
    {
        var context = new TestPayloadContext(BuildProject(new[]
        {
            new DiagramMeta { Id = "diag-a", Name = "Unit A", Mode = "Main", ControlState = "Auto", UnitId = "unit-a", Unit = "Unit A" },
            new DiagramMeta { Id = "diag-b", Name = "Unit B", Mode = "Manual", ControlState = "Manual", UnitId = "unit-b", Unit = "Unit B" }
        }, new[]
        {
            new Unit { Id = "unit-a", Name = "Unit A" },
            new Unit { Id = "unit-b", Name = "Unit B" }
        }));

        var payload = GrafcetStudioCodegenPayload.buildCSharpPayload(context, "twincat-st", "__all__");

        Assert.Equal(2, payload.Units.Count);
        Assert.Equal(2, payload.Flows.Count);
        Assert.Contains(payload.Flows, flow => flow.Diagram?.UnitId == "unit-a");
        Assert.Contains(payload.Flows, flow => flow.Diagram?.UnitId == "unit-b");
    }

    [Fact]
    public void MultiFileGenerator_ProjectScopedPayload_EmitsOneUnitFilePerUnit()
    {
        var generator = new MultiFileGenerator(
            new UnitConfigGenerator(new TemplateManager(Handlebars.Create()), new SequenceResolver()),
            new ErrorGenerator(),
            new DeviceManagerGenerator(),
            new SystemControlGenerator());

        var payload = BuildPayload();
        payload.Unit = null;
        payload.Units = new List<UnitInfo>
        {
            new() { Id = "unit-a", Name = "Unit A", Label = "Unit A" },
            new() { Id = "unit-b", Name = "Unit B", Label = "Unit B" }
        };
        payload.Flows = new List<FlowInfo>
        {
            new()
            {
                Id = "flow-a",
                Name = "Flow A",
                Diagram = new DiagramInfo { Id = "diag-a", UnitId = "unit-a", Unit = "Unit A", BaseMr = 100 },
                Steps = new List<Step> { new() { Id = "step-a", Number = 1, ExecAddress = "@MR100", DoneAddress = "@MR101" } },
                Transitions = new List<Transition>()
            },
            new()
            {
                Id = "flow-b",
                Name = "Flow B",
                Diagram = new DiagramInfo { Id = "diag-b", UnitId = "unit-b", Unit = "Unit B", BaseMr = 200 },
                Steps = new List<Step> { new() { Id = "step-b", Number = 1, ExecAddress = "@MR200", DoneAddress = "@MR201" } },
                Transitions = new List<Transition>()
            }
        };

        var files = generator.GenerateFiles(payload).ToList();

        Assert.Contains(files, file => file.Path == "Units/Unit_Unit_A.st");
        Assert.Contains(files, file => file.Path == "Units/Unit_Unit_B.st");
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
    public void BuildCSharpPayload_IncludesMacroPayloadFields()
    {
        var context = new TestPayloadContext(BuildProject(new[]
        {
            new DiagramMeta
            {
                Id = "diag-macro",
                Name = "Pick",
                DiagramType = "Macro",
                UnitId = "unit-1",
                Unit = "Main"
            }
        }))
        {
            StepsByDiagram = new Dictionary<string, List<Step>>
            {
                ["diag-macro"] = new()
                {
                    new() { Id = "s10", Number = 10, Kind = "macro", MacroFlowId = "diag-macro-step" }
                }
            }
        };

        var payload = GrafcetStudioCodegenPayload.buildCSharpPayload(context, "twincat-st", "unit-1");
        var flow = Assert.Single(payload.Flows);
        var step = Assert.Single(flow.Steps);

        Assert.Equal("Macro", flow.DiagramType);
        Assert.Equal("Macro", flow.Diagram?.DiagramType);
        Assert.Equal("unit-1", flow.Diagram?.UnitId);
        Assert.Equal("Main", flow.Diagram?.Unit);
        Assert.Equal("macro", step.Kind);
        Assert.Equal("diag-macro-step", step.MacroFlowId);
    }

    [Fact]
    public void BuildCSharpPayload_ResolvesAllDeviceTypes()
    {
        var context = new TestPayloadContext(BuildProject(Array.Empty<DiagramMeta>(), deviceTypes: new[]
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


    [Fact]
    public void UnitConfigGenerator_OneMacroCallsOneMacroStep_GeneratesMacroPort()
    {
        var output = BuildUnitConfigGenerator().GenerateUnitContent(BuildMacroPayload());

        Assert.Contains("Clamp_Port", output);
        Assert.Contains("macroBindings", output);
    }

    [Fact]
    public void UnitConfigGenerator_MacroStepCalledTwice_ReturnsError()
    {
        var payload = BuildMacroPayload();
        payload.Flows[0].Steps.Add(new Step { Id = "s20", Label = "S20", Number = 20, Kind = "macro", MacroFlowId = "flow-step", ExecAddress = "@MR104", DoneAddress = "@MR105" });

        var ex = Assert.Throws<InvalidOperationException>(() => BuildUnitConfigGenerator().GenerateUnitContent(payload));
        Assert.Contains("referenced by multiple macro steps", ex.Message);
    }

    [Fact]
    public void UnitConfigGenerator_MacroStepFromAnotherUnit_ReturnsError()
    {
        var payload = BuildMacroPayload();
        payload.Flows[1].Diagram!.UnitId = "unit-b";

        var ex = Assert.Throws<InvalidOperationException>(() => BuildUnitConfigGenerator().GenerateUnitContent(payload));
        Assert.Contains("from another unit", ex.Message);
    }

    [Fact]
    public void UnitConfigGenerator_MacroStepContainsMacroStep_ReturnsError()
    {
        var payload = BuildMacroPayload();
        payload.Flows[1].Steps.Add(new Step { Id = "nested", Label = "Nested", Number = 99, Kind = "macro", MacroFlowId = "other" });

        var ex = Assert.Throws<InvalidOperationException>(() => BuildUnitConfigGenerator().GenerateUnitContent(payload));
        Assert.Contains("cannot contain nested macro steps", ex.Message);
    }

    [Fact]
    public void UnitConfigGenerator_OldProjectDefaultsToMacroNormal_Generates()
    {
        var payload = BuildPayload();
        payload.Flows[0].Id = "old-flow";
        payload.Flows[0].Name = "Old";
        payload.Flows[0].DiagramType = null;
        payload.Flows[0].Diagram = new DiagramInfo { Id = "old-diag", UnitId = "unit-1", Unit = "Main", BaseMr = 100 };
        payload.Flows[0].Steps = new List<Step> { new() { Id = "old-s1", Number = 1, ExecAddress = "@MR100", DoneAddress = "@MR101" } };

        var output = BuildUnitConfigGenerator().GenerateUnitContent(payload);

        Assert.Contains("old-flow", output);
        Assert.DoesNotContain("cannot contain nested macro steps", output);
    }

    [Fact]
    public void UnitConfigGenerator_MacroStepWithMacroPortVariable_UsesVariablePortData()
    {
        var payload = BuildMacroPayload();
        payload.Variables.Add(new DeviceVariable
        {
            Label = "Clamp",
            Format = "MacroPort",
            SignalAddresses = new Dictionary<string, string>
            {
                ["Enable"] = "@MR300",
                ["Start"] = "@MR301",
                ["Busy"] = "@MR302",
                ["Done"] = "@MR303",
                ["Error"] = "@MR304",
                ["Reset"] = "@MR305"
            }
        });

        var output = BuildUnitConfigGenerator().GenerateUnitContent(payload);

        Assert.Contains("MacroPort", output);
        Assert.Contains("@MR301", output);
        Assert.Contains("macroPortVariable", output);
    }

    [Fact]
    public void UnitConfigGenerator_MacroStepWithWrongFormatVariable_ReturnsError()
    {
        var payload = BuildMacroPayload();
        payload.Variables.Add(new DeviceVariable { Label = "Clamp", Format = "Cylinder" });

        var ex = Assert.Throws<InvalidOperationException>(() => BuildUnitConfigGenerator().GenerateUnitContent(payload));
        Assert.Contains("expected MacroPort", ex.Message);
    }

    [Fact]
    public void UnitConfigGenerator_MacroStepDuplicateVariableName_ReturnsError()
    {
        var payload = BuildMacroPayload();
        payload.Variables.Add(new DeviceVariable { Label = "Clamp", Format = "MacroPort" });
        payload.Variables.Add(new DeviceVariable { Label = "Clamp", Format = "MacroPort" });

        var ex = Assert.Throws<InvalidOperationException>(() => BuildUnitConfigGenerator().GenerateUnitContent(payload));
        Assert.Contains("Duplicate MacroPort variable name", ex.Message);
    }

    private static int CountOccurrences(string value, string search)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(search, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += search.Length;
        }

        return count;
    }
    private static void AssertInvalidSiemensTemplate(string json, params string[] expectedMessages)
    {
        var templateRoot = Path.Combine(Path.GetTempPath(), "grafcetstudio-invalid-siemens-lad-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(templateRoot);
        File.WriteAllText(Path.Combine(templateRoot, "siemens-lad.json"), json);

        try
        {
            var payload = BuildPayload();
            payload.TemplateRootPath = templateRoot;

            var ex = Assert.Throws<InvalidOperationException>(() => new SiemensLadDslGenerator().GenerateFiles(payload).ToList());
            foreach (var expected in expectedMessages)
            {
                Assert.Contains(expected, ex.Message);
            }
        }
        finally
        {
            if (Directory.Exists(templateRoot)) Directory.Delete(templateRoot, true);
        }
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


    private static UnitConfigGenerator BuildUnitConfigGenerator()
        => new(new TemplateManager(Handlebars.Create()), new SequenceResolver());

    private static CodegenPayload BuildMacroPayload()
        => new()
        {
            Project = new ProjectInfo { Name = "Demo" },
            Unit = new UnitInfo { Id = "unit-a", Name = "Main", Label = "Main" },
            Variables = new List<DeviceVariable>(),
            DeviceTypes = new List<DeviceType>(),
            Flows = new List<FlowInfo>
            {
                new()
                {
                    Id = "flow-macro",
                    Name = "Pick",
                    DiagramType = "Macro",
                    Diagram = new DiagramInfo { Id = "diag-macro", UnitId = "unit-a", Unit = "Main", BaseMr = 100 },
                    Steps = new List<Step>
                    {
                        new() { Id = "s10", Label = "S10", Number = 10, Kind = "macro", MacroFlowId = "flow-step", ExecAddress = "@MR100", DoneAddress = "@MR101" }
                    },
                    Transitions = new List<Transition>
                    {
                        new() { Id = "t1", Label = "T1", Condition = "I0.0" },
                        new() { Id = "t2", Label = "T2", Condition = "I0.1" }
                    }
                },
                new()
                {
                    Id = "flow-step",
                    Name = "Clamp",
                    DiagramType = "MacroStep",
                    Diagram = new DiagramInfo { Id = "diag-step", UnitId = "unit-a", Unit = "Main", BaseMr = 200 },
                    Steps = new List<Step>
                    {
                        new() { Id = "s1", Label = "S1", Number = 1, ExecAddress = "@MR200", DoneAddress = "@MR201" }
                    },
                    Transitions = new List<Transition>
                    {
                        new() { Id = "t1", Label = "T1", Condition = "I0.0" },
                        new() { Id = "t2", Label = "T2", Condition = "I0.1" }
                    }
                }
            }
        };
    private sealed class TestPayloadContext : GrafcetStudioCodegenPayload.PayloadContext
    {
        public TestPayloadContext(Project project)
        {
            Project = project;
        }

        public Project Project { get; }
        public ProjectVariables? ProjectVariables { get; set; }
        public Dictionary<string, List<Step>> StepsByDiagram { get; set; } = new();

        public GrafcetStudioCodegenPayload.CodegenAssets GetAssets() => new()
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
                Steps = StepsByDiagram.TryGetValue(diagramId, out var steps) ? steps : new List<Step> { new() { Id = diagramId + "-s1", Number = 1 } },
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
        StoredDiagramData? GrafcetStudioCodegenPayload.PayloadContext.loadDiagramData(string diagramId) => LoadDiagramData(diagramId);
        GrafcetStudioCodegenPayload.CodegenAssets GrafcetStudioCodegenPayload.PayloadContext.getAssets() => GetAssets();
        object? GrafcetStudioCodegenPayload.PayloadContext.ensureFlowAddressConfig(DiagramMeta diagram, bool assignUniqueBase) => null;
        ProjectVariables GrafcetStudioCodegenPayload.PayloadContext.ensureProjectVariables() => EnsureProjectVariables();
        bool GrafcetStudioCodegenPayload.PayloadContext.syncVariableSignalAddressesFromDeviceTypes() => SyncVariableSignalAddressesFromDeviceTypes();
        void GrafcetStudioCodegenPayload.PayloadContext.saveProject() => SaveProject();
        string GrafcetStudioCodegenPayload.PayloadContext.getDefaultUnitId() => GetDefaultUnitId();
        DeviceSignal[] GrafcetStudioCodegenPayload.PayloadContext.unitSignals => UnitSignals;
        DeviceSignal[] GrafcetStudioCodegenPayload.PayloadContext.projectUnitStructSignals => ProjectUnitStructSignals;
    }
}











