using GrafcetStudio.App.Generators;
using GrafcetStudio.CodeGen.Runtime.Models;
using GrafcetStudio.CodeGen.Template;
using GrafcetStudio.App.Generators.Siemens;
using GrafcetStudio.Domain.Resolution;
using HandlebarsDotNet;
using GrafcetStudio.Domain.Models;
using GrafcetStudio.Domain.Enums;
using Xunit;
using System.IO;

namespace GrafcetStudio.App.Tests;

public class GeneratorSmokeTests
{
    [Fact]
    public void MapIOGenerator_ReturnsExpectedSections()
    {
        var generator = new MapIOGenerator();
        var payload = BuildPayload();
        var output = generator.Generate(payload, Array.Empty<AggregatedOutputBinding>());
        Assert.Contains("ioMapping", output);
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

        var orchestratorOutput = generator.GenerateOrchestrator(payload);
        Assert.Contains("orchestratorFlows", orchestratorOutput);
        Assert.Contains("skeleton", orchestratorOutput);
        var systemOutput = generator.GenerateSystem(payload);
        Assert.Contains("\"flows\"", systemOutput);
    }


    [Fact]
    public void SiemensLadDslGenerator_CustomHbsTextTemplateOverridesDefaultTemplate()
    {
        var templateRoot = Path.Combine(Path.GetTempPath(), "grafcetstudio-siemens-lad-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(templateRoot);
        File.WriteAllText(Path.Combine(templateRoot, "default.lad.hbs"), """
        TIA_VERSION 17
        BLOCK_NAME DefaultTemplate_Should_Not_Be_Used
        BLOCK_NUMBER 99

        NETWORK default
        TITLE DefaultTemplateNetwork
        EXPR M9.0
        COIL REF M9.1
        END
        """);
        File.WriteAllText(Path.Combine(templateRoot, "siemens-lad.hbs"), """
        TIA_VERSION 17
        BLOCK_NAME {{unit.label}}_CustomOverride_LAD
        BLOCK_NUMBER 10

        NETWORK activation
        TITLE CustomOverrideActivation
        COMMENT custom root siemens-lad.hbs override
        EXPR M0.1 & M0.2
        COIL REF M0.0
        END
        """);

        try
        {
            var payload = BuildPayload();
            payload.TemplateRootPath = templateRoot;
            payload.Unit = new UnitInfo { Id = "unit-1", Name = "Main Unit", Label = "MainUnit" };

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
    public void SiemensLadTextTemplateParser_ParsesNetworkFieldsAndExpression()
    {
        var template = SiemensLadTextTemplateParser.Parse("""
        TIA_VERSION 17
        BLOCK_NAME Demo_LAD
        BLOCK_NUMBER 3

        NETWORK n1
        TITLE Activate step
        STATUS ready
        COMMENT first comment line
        COMMENT second comment line
        EXPR M0.0 & (I0.0 | I0.1)
        SET_COIL REF M0.1
        END
        """, "inline.lad.hbs");

        Assert.Equal((uint)17, template.TiaVersion);
        Assert.Equal("Demo_LAD", template.BlockName);
        Assert.Equal((uint)3, template.BlockNumber);
        var network = Assert.Single(template.Networks);
        Assert.Equal("n1", network.Id);
        Assert.Equal("Activate step", network.Title);
        Assert.Equal("ready", network.Status);
        Assert.Contains("first comment line", network.Comment);
        Assert.Contains("second comment line", network.Comment);
        Assert.Equal("set_coil", network.Output!.Type);
        Assert.Equal("M0.1", network.Output.Ref);
        Assert.Equal("AND", network.Expression!.Type);
    }

    [Fact]
    public void SiemensLadDslGenerator_RepeatNetworksUseRenderedHbsTextContext()
    {
        var templateRoot = Path.Combine(Path.GetTempPath(), "grafcetstudio-siemens-lad-repeat-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(templateRoot);
        File.WriteAllText(Path.Combine(templateRoot, "siemens-lad.hbs"), """
        TIA_VERSION 17
        BLOCK_NAME {{unit.label}}_Repeat_LAD
        BLOCK_NUMBER 11

        {{#each flows}}
        {{#each steps}}
        NETWORK step_{{id}}
        TITLE Step {{../name}} {{number}} {{label}}
        COMMENT Done for {{label}}
        EXPR {{execAddress}}
        COIL REF {{doneAddress}}
        END

        {{#each actions}}
        NETWORK action_{{../id}}_{{@index}}
        TITLE Action {{variable}} on {{../label}}
        COMMENT Action variable {{variable}}
        EXPR {{../execAddress}}
        COIL REF {{address}}
        END
        {{/each}}
        {{/each}}

        {{#each transitions}}
        NETWORK transition_{{id}}
        TITLE Transition {{label}} in {{../name}}
        COMMENT Condition {{label}}
        EXPR {{condition}}
        COIL REF {{condition}}
        END
        {{/each}}
        {{/each}}
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
    public void SiemensLadDslGenerator_SetAndResetCoils_GenerateXml()
    {
        var templateRoot = Path.Combine(Path.GetTempPath(), "grafcetstudio-siemens-lad-set-reset-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(templateRoot);
        File.WriteAllText(Path.Combine(templateRoot, "siemens-lad.hbs"), """
        TIA_VERSION 17
        BLOCK_NAME Main_SetReset_LAD

        NETWORK set_done
        TITLE SetDone
        EXPR M0.0
        SET_COIL REF M0.1
        END

        NETWORK reset_exec
        TITLE ResetExec
        EXPR M0.1
        RESET_COIL REF M0.0
        END
        """);

        try
        {
            var payload = BuildPayload();
            payload.TemplateRootPath = templateRoot;

            var file = Assert.Single(new SiemensLadDslGenerator().GenerateFiles(payload));

            Assert.Equal("Main_SetReset_LAD.xml", file.Path);
            Assert.Contains("SetDone", file.Content);
            Assert.Contains("ResetExec", file.Content);
            Assert.Contains("Name=\"SCoil\"", file.Content);
            Assert.Contains("Name=\"RCoil\"", file.Content);
        }
        finally
        {
            if (Directory.Exists(templateRoot)) Directory.Delete(templateRoot, true);
        }
    }

    [Fact]
    public void SiemensLadDslGenerator_InvalidRenderedText_ReturnsNetworkAndLineError()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => SiemensLadTextTemplateParser.Parse("""
        TIA_VERSION 17
        BLOCK_NAME Bad_LAD

        NETWORK n1
        TITLE BadExpr
        EXPR M0.0 $
        COIL REF M0.1
        END
        """, "bad.lad.hbs"));

        Assert.Contains("bad.lad.hbs", ex.Message);
        Assert.Contains("Network 'n1'", ex.Message);
        Assert.Contains("line 6", ex.Message);
        Assert.Contains("Invalid ladder expression", ex.Message);
        Assert.Contains("unexpected '$'", ex.Message);
    }

    [Fact]
    public void SiemensLadDslGenerator_DoesNotFallbackToJsonTemplate()
    {
        var templateRoot = Path.Combine(Path.GetTempPath(), "grafcetstudio-siemens-lad-json-not-used-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(templateRoot);
        File.WriteAllText(Path.Combine(templateRoot, "siemens-lad.json"), """
        {
          "platform": "siemens-lad",
          "blockName": "Json_Should_Not_Be_Used",
          "networks": []
        }
        """);

        try
        {
            var payload = BuildPayload();
            payload.TemplateRootPath = templateRoot;

            var ex = Assert.Throws<FileNotFoundException>(() => new SiemensLadDslGenerator().GenerateFiles(payload).ToList());
            Assert.Contains("Cannot find Siemens LAD HBS text template", ex.Message);
            Assert.Contains("siemens-lad.hbs", ex.Message);
            Assert.Contains("default.lad.hbs", ex.Message);
            Assert.DoesNotContain("siemens-lad.json", ex.Message);
        }
        finally
        {
            if (Directory.Exists(templateRoot)) Directory.Delete(templateRoot, true);
        }
    }

    [Fact]
    public void SiemensLadDslGenerator_UnsupportedInstructionReportsClearError()
    {
        var templateRoot = Path.Combine(Path.GetTempPath(), "grafcetstudio-siemens-lad-ton-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(templateRoot);
        File.WriteAllText(Path.Combine(templateRoot, "siemens-lad.hbs"), """
        TIA_VERSION 17
        BLOCK_NAME Main_TON_LAD

        NETWORK timer
        TITLE Timer
        STATUS partial
        EXPR M0.0
        TON T1
        PT LIT T#5s
        Q REF M0.1
        END_TON
        END
        """);

        try
        {
            var payload = BuildPayload();
            payload.TemplateRootPath = templateRoot;

            var ex = Assert.Throws<InvalidOperationException>(() => new SiemensLadDslGenerator().GenerateFiles(payload).ToList());
            Assert.Contains("Network 'timer'", ex.Message);
            Assert.Contains("instruction 'TON'", ex.Message);
            Assert.Contains("XML generation is not implemented yet", ex.Message);
        }
        finally
        {
            if (Directory.Exists(templateRoot)) Directory.Delete(templateRoot, true);
        }
    }

    [Fact]
    public void BuildCSharpPayload_KeepsLegacyControlStateAsAuto()
    {
        var context = new TestPayloadContext(BuildProject(new[]
        {
            new DiagramMeta { Id = "diag-legacy", Name = "Legacy", Mode = "Main", UnitId = "unit-1" }
        }));

        var payload = GrafcetStudioCodegenPayload.buildCSharpPayload(context, "Keyence", "unit-1");

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

        var payloadA = GrafcetStudioCodegenPayload.buildCSharpPayload(context, "Keyence", "unit-a");
        var payloadB = GrafcetStudioCodegenPayload.buildCSharpPayload(context, "Keyence", "unit-b");

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

        var payload = GrafcetStudioCodegenPayload.buildCSharpPayload(context, "Keyence", "__all__");

        Assert.Equal(2, payload.Units.Count);
        Assert.Equal(2, payload.Flows.Count);
        Assert.Contains(payload.Flows, flow => flow.Diagram?.UnitId == "unit-a");
        Assert.Contains(payload.Flows, flow => flow.Diagram?.UnitId == "unit-b");
    }

    [Fact]
    public void MultiFileGenerator_ProjectScopedPayload_EmitsOneUnitFilePerUnit()
    {
        var generator = new MultiFileGenerator(
            new KeyenceGenerator(new TemplateManager(Handlebars.Create()), new SequenceResolver()),
            new ErrorGenerator(),
            new DeviceManagerGenerator(),
            new SystemControlGenerator(),
            new MapIOGenerator());

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
                Diagram = new DiagramInfo { Id = "diag-a", UnitId = "unit-a", Unit = "Unit A", BaseMr = "100" },
                Steps = new List<Step> { new() { Id = "step-a", Number = 1, ExecAddress = "@MR100", DoneAddress = "@MR101" } },
                Transitions = new List<Transition>()
            },
            new()
            {
                Id = "flow-b",
                Name = "Flow B",
                Diagram = new DiagramInfo { Id = "diag-b", UnitId = "unit-b", Unit = "Unit B", BaseMr = "200" },
                Steps = new List<Step> { new() { Id = "step-b", Number = 1, ExecAddress = "@MR200", DoneAddress = "@MR201" } },
                Transitions = new List<Transition>()
            }
        };

        var files = generator.GenerateFiles(payload).ToList();

        Assert.Contains(files, file => file.Path == "Units/Unit_Unit_A.mnm");
        Assert.Contains(files, file => file.Path == "Units/Unit_Unit_B.mnm");
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

        var payload = GrafcetStudioCodegenPayload.buildCSharpPayload(context, "Keyence", "unit-1");

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

        var payload = GrafcetStudioCodegenPayload.buildCSharpPayload(context, "Keyence", "unit-1");
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

        var payload = GrafcetStudioCodegenPayload.buildCSharpPayload(context, "Keyence", "unit-1");

        Assert.Equal(2, payload.DeviceTypes.Count);
    }


    [Fact]
    public void KeyenceGenerator_Phase1_PopulatesStepMnemonicsInJsonContext()
    {
        var payload = new CodegenPayload
        {
            Project = new ProjectInfo { Name = "Demo" },
            Unit = new UnitInfo { Id = "unit-1", Name = "Main", Label = "Main" },
            Variables = new List<DeviceVariable>
            {
                new()
                {
                    Label = "Motor1",
                    Format = "Motor",
                    SignalAddresses = new Dictionary<string, string>
                    {
                        ["Run"] = "MR10",
                        ["Done"] = "MR11"
                    }
                }
            },
            DeviceTypes = new List<DeviceType>
            {
                new() { Name = "Motor", Signals = new List<DeviceSignal> { new() { Id = "run", Name = "Run" }, new() { Id = "done", Name = "Done" } } }
            },
            Flows = new List<FlowInfo>
            {
                new()
                {
                    Id = "flow-1",
                    Name = "AutoFlow",
                    Type = "auto",
                    DiagramType = "Macro",
                    Diagram = new DiagramInfo { Id = "diag-1", Name = "AutoFlow", UnitId = "unit-1", Unit = "Main", BaseMr = "100" },
                    Steps = new List<Step>
                    {
                        new()
                        {
                            Id = "s1",
                            Number = 1,
                            Label = "Start",
                            IsInitial = true,
                            ExecAddress = "@MR100",
                            DoneAddress = "@MR101",
                            Actions = new List<StepAction>
                            {
                                new() { Variable = "Motor1.Run", Qualifier = GrafcetStudio.Domain.Enums.ActionQualifier.N, Complete = new StepActionCompletion { Sensor = "Done", SensorLabel = "Motor1.Done", Address = "MR11" } }
                            }
                        }
                    },
                    Transitions = new List<Transition>
                    {
                        new() { Id = "t1", Label = "T1", Condition = "!MR20", FromStepIds = new List<string>{ "s1" }, ToStepIds = new List<string>() }
                    }
                }
            }
        };

        var output = BuildUnitConfigGenerator().GenerateUnitContent(payload);

        Assert.Contains("\"activationMnemonic\": \"SET  @MR100\"", output);
        Assert.Contains("LD   @MR100", output);
        Assert.Contains("AND  MR11", output);
        Assert.Contains("ANB  MR20", output);
        Assert.Contains("SET  @MR101", output);
        Assert.Contains("OUT  MR10", output);
        Assert.Contains("\"doneMnemonic\"", output);
        Assert.Contains("\"bodyMnemonic\"", output);
    }


    [Fact]
    public void KeyenceGenerator_TemplateExpressionStylePostProcessesSingleOutputRungsToMnemonic()
    {
        var templates = new TemplateManager(Handlebars.Create());
        templates.LoadTemplate("uc.auto", """
        ; Expression-style template keeps HBS readable
        {{#each autoFlows}}
        {{#each steps}}
        {{expression.activationExpression}} -> SET {{ExecAddress}}
        {{#each expression.outputs}}
        {{expression}}
        {{/each}}
        {{expression.doneExpression}}
        {{/each}}
        {{/each}}
        """);

        var payload = new CodegenPayload
        {
            Project = new ProjectInfo { Name = "Demo" },
            Unit = new UnitInfo { Id = "unit-1", Name = "Main", Label = "Main" },
            Variables = new List<DeviceVariable>
            {
                new()
                {
                    Label = "Motor1",
                    Format = "Motor",
                    SignalAddresses = new Dictionary<string, string>
                    {
                        ["Run"] = "MR10"
                    }
                }
            },
            DeviceTypes = new List<DeviceType>
            {
                new() { Name = "Motor", Signals = new List<DeviceSignal> { new() { Id = "run", Name = "Run" } } }
            },
            Flows = new List<FlowInfo>
            {
                new()
                {
                    Id = "flow-1",
                    Name = "AutoFlow",
                    Type = "auto",
                    Mode = "auto",
                    DiagramType = "Macro",
                    Diagram = new DiagramInfo { Id = "diag-1", Name = "AutoFlow", UnitId = "unit-1", Unit = "Main", BaseMr = "100" },
                    Steps = new List<Step>
                    {
                        new()
                        {
                            Id = "s1",
                            Number = 1,
                            Label = "Start",
                            IsInitial = true,
                            ExecAddress = "MR100",
                            DoneAddress = "MR101",
                            Actions = new List<StepAction>
                            {
                                new() { Variable = "Motor1.Run", Qualifier = ActionQualifier.N }
                            }
                        }
                    }
                }
            }
        };

        var output = new KeyenceGenerator(templates, new SequenceResolver()).GenerateUnitContent(payload);

        Assert.Contains("LD   MR100", output);
        Assert.Contains("OUT  MR10", output);
        Assert.Contains("SET  MR101", output);
        Assert.DoesNotContain("->", output);
        Assert.DoesNotContain("MR100 -> OUT MR10", output);
    }
    [Fact]
    public void KeyenceGenerator_Phase2_PopulatesDeviceOutputMnemonicsInJsonContext()
    {
        var libraryPath = Path.Combine(Path.GetTempPath(), $"grafcet-device-library-{Guid.NewGuid():N}.json");
        File.WriteAllText(libraryPath, """
            {
              "devices": [
                {
                  "deviceId": "Cylinder",
                  "name": "Cylinder",
                  "commands": {
                    "CoilA": {
                      "actionLabel": "Extend",
                      "driveSignal": "CoilA",
                      "interlock": {
                        "signal": "LockA",
                        "label": "Cylinder1.LockA",
                        "requiredState": "1"
                      }
                    }
                  }
                }
              ]
            }
            """);

        try
        {
            var payload = new CodegenPayload
            {
                Project = new ProjectInfo { Name = "Demo" },
                Unit = new UnitInfo { Id = "unit-1", Name = "Main", Label = "Main" },
                DeviceLibraryPath = libraryPath,
                Variables = new List<DeviceVariable>
                {
                    new()
                    {
                        Label = "Main",
                        Format = "Unit",
                        SignalAddresses = new Dictionary<string, string>
                        {
                            ["flagAuto"] = "MR1",
                            ["flagManual"] = "MR2",
                            ["flagOrigin"] = "MR3"
                        }
                    },
                    new()
                    {
                        Label = "Cylinder1",
                        Format = "Cylinder",
                        SignalAddresses = new Dictionary<string, string>
                        {
                            ["CoilA"] = "MR10",
                            ["LockA"] = "MR20"
                        }
                    }
                },
                DeviceTypes = new List<DeviceType>
                {
                    new()
                    {
                        Name = "Cylinder",
                        Signals = new List<DeviceSignal>
                        {
                            new() { Id = "coilA", Name = "CoilA" },
                            new() { Id = "lockA", Name = "LockA" }
                        }
                    }
                },
                Flows = new List<FlowInfo>
                {
                    new()
                    {
                        Id = "flow-1",
                        Name = "AutoFlow",
                        Type = "auto",
                        Mode = "auto",
                        DiagramType = "Macro",
                        Diagram = new DiagramInfo { Id = "flow-1", Name = "AutoFlow", UnitId = "unit-1", Unit = "Main", Mode = "auto", BaseMr = "100" },
                        Steps = new List<Step>
                        {
                            new()
                            {
                                Id = "s1",
                                Number = 1,
                                Label = "AutoStep",
                                IsInitial = true,
                                ExecAddress = "MR100",
                                DoneAddress = "MR101",
                                Actions = new List<StepAction>
                                {
                                    new() { Variable = "Cylinder1.CoilA", Qualifier = ActionQualifier.N }
                                }
                            }
                        }
                    },
                    new()
                    {
                        Id = "flow-2",
                        Name = "OriginFlow",
                        Type = "origin",
                        Mode = "origin",
                        DiagramType = "Macro",
                        Diagram = new DiagramInfo { Id = "flow-2", Name = "OriginFlow", UnitId = "unit-1", Unit = "Main", Mode = "origin", BaseMr = "200" },
                        Steps = new List<Step>
                        {
                            new()
                            {
                                Id = "s2",
                                Number = 2,
                                Label = "OriginStep",
                                IsInitial = true,
                                ExecAddress = "MR200",
                                DoneAddress = "MR201",
                                Actions = new List<StepAction>
                                {
                                    new() { Variable = "Cylinder1.CoilA", Qualifier = ActionQualifier.N }
                                }
                            }
                        }
                    }
                }
            };

            var output = BuildUnitConfigGenerator().GenerateUnitContent(payload);

            Assert.Contains("\"mnemonic\"", output);
            Assert.Contains("\"conditionMnemonic\"", output);
            Assert.Contains("LD   MR1", output);
            Assert.Contains("AND  MR100", output);
            Assert.Contains("ANB  MR101", output);
            Assert.Contains("LD   MR3", output);
            Assert.Contains("AND  MR200", output);
            Assert.Contains("ANB  MR201", output);
            Assert.Contains("ORL", output);
            Assert.Contains("AND  MR20", output);
            Assert.Contains("OUT  MR10", output);
        }
        finally
        {
            if (File.Exists(libraryPath)) File.Delete(libraryPath);
        }
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
        payload.Flows[0].Diagram = new DiagramInfo { Id = "old-diag", UnitId = "unit-1", Unit = "Main", BaseMr = "100" };
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


    private static KeyenceGenerator BuildUnitConfigGenerator()
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
                    Diagram = new DiagramInfo { Id = "diag-macro", UnitId = "unit-a", Unit = "Main", BaseMr = "100" },
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
                    Diagram = new DiagramInfo { Id = "diag-step", UnitId = "unit-a", Unit = "Main", BaseMr = "200" },
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






















