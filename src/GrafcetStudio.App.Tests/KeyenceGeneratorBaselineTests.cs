using GrafcetStudio.App.Generators;
using GrafcetStudio.CodeGen.Runtime.Models;
using GrafcetStudio.CodeGen.Template;
using GrafcetStudio.Domain.Resolution;
using GrafcetStudio.Domain.Models;
using GrafcetStudio.Domain.Enums;
using HandlebarsDotNet;
using Xunit;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace GrafcetStudio.App.Tests;

public class KeyenceGeneratorBaselineTests
{
    private static readonly string GoldenFilePath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "../../../KeyenceGoldenOutput.mnm"
    );

    [Fact]
    public void GenerateUnitContent_MatchesGoldenSnapshot()
    {
        var templates = new TemplateManager(Handlebars.Create());
        
        // Load default/standard templates so we render actual sections
        templates.LoadTemplate("uc.unitAuto", "; AUTO FLOWS\n{{#each autoFlows}}\n; Flow: {{name}} (Min: {{stepMinAddress}}, Max: {{stepMaxAddress}})\n{{#each steps}}\n; Step {{number}} ({{label}})\n{{expression.activationMnemonic}}\n{{expression.outputMnemonic}}\n{{expression.doneMnemonic}}\n{{/each}}\n{{/each}}");
        templates.LoadTemplate("uc.unitOrigin", "; ORIGIN FLOWS\n{{#each originFlows}}\n; Flow: {{name}}\n{{#each steps}}\n; Step {{number}} ({{label}})\n{{expression.activationMnemonic}}\n{{expression.outputMnemonic}}\n{{expression.doneMnemonic}}\n{{/each}}\n{{/each}}");
        templates.LoadTemplate("uc.outputs", "; DEVICE OUTPUTS\n{{#each deviceOutputGroups}}\n; Device: {{deviceLabel}} (Kind: {{deviceKind}}, Address: {{address}})\n{{#each commands}}\n; Command: {{commandId}} (Action: {{actionLabel}}, Target: {{target}})\n{{mnemonic}}\n{{/each}}\n{{/each}}");

        var generator = new KeyenceGenerator(templates, new SequenceResolver());
        var payload = BuildComprehensivePayload();
        
        // Write a temp library file for testing
        var libraryPath = Path.Combine(Path.GetTempPath(), $"baseline-device-library-{Guid.NewGuid():N}.json");
        File.WriteAllText(libraryPath, """
        {
          "devices": [
            {
              "deviceId": "Cylinder",
              "name": "Cylinder",
              "commands": {
                "Extend": {
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
        payload.DeviceLibraryPath = libraryPath;

        try
        {
            var output = generator.GenerateUnitContent(payload);
            
            // Normalize line endings to LF
            var normalizedOutput = output.Replace("\r\n", "\n").Trim();

            // If the golden file does not exist, create it (bootstrap)
            if (!File.Exists(GoldenFilePath))
            {
                File.WriteAllText(GoldenFilePath, normalizedOutput);
            }

            var goldenContent = File.ReadAllText(GoldenFilePath).Replace("\r\n", "\n").Trim();

            if (normalizedOutput != goldenContent)
            {
                var diffPath = Path.Combine(Path.GetTempPath(), "KeyenceGoldenOutput_Diff.mnm");
                File.WriteAllText(diffPath, normalizedOutput);
                Assert.Fail($"Output did not match Golden Snapshot. Actual output written to {diffPath}. Run a diff tool between it and the golden file.");
            }
        }
        finally
        {
            if (File.Exists(libraryPath))
            {
                File.Delete(libraryPath);
            }
        }
    }

    private static CodegenPayload BuildComprehensivePayload()
    {
        return new CodegenPayload
        {
            Project = new ProjectInfo { Name = "BaselineDemo" },
            Unit = new UnitInfo { Id = "unit-1", Name = "MainUnit", Label = "MainUnit" },
            Variables = new List<DeviceVariable>
            {
                new()
                {
                    Label = "MainUnit",
                    Format = "Unit",
                    SignalAddresses = new Dictionary<string, string>
                    {
                        ["flagAuto"] = "MR0",
                        ["flagManual"] = "MR1",
                        ["flagOrigin"] = "MR2"
                    }
                },
                new()
                {
                    Label = "Cylinder1",
                    Format = "Cylinder",
                    Address = "MR10",
                    SignalAddresses = new Dictionary<string, string>
                    {
                        ["CoilA"] = "MR100",
                        ["LockA"] = "MR101",
                        ["ExtendDone"] = "MR102"
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
                        new() { Id = "lockA", Name = "LockA" },
                        new() { Id = "extendDone", Name = "ExtendDone" }
                    }
                }
            },
            Flows = new List<FlowInfo>
            {
                // Auto Flow (Linear flow with device output and macro call)
                new()
                {
                    Id = "flow-auto",
                    Name = "AutoFlow",
                    Type = "auto",
                    Mode = "auto",
                    DiagramType = "Macro",
                    Diagram = new DiagramInfo { Id = "diag-auto", UnitId = "unit-1", Unit = "MainUnit", BaseMr = "1000", BoolAddressMode = "linear" },
                    Steps = new List<Step>
                    {
                        new()
                        {
                            Id = "s1",
                            Number = 1,
                            Label = "Step1",
                            IsInitial = true,
                            ExecAddress = "MR1000",
                            DoneAddress = "MR1001",
                            Actions = new List<StepAction>
                            {
                                new()
                                {
                                    Variable = "Cylinder1.Extend",
                                    Qualifier = ActionQualifier.N,
                                    Complete = new StepActionCompletion
                                    {
                                        Sensor = "ExtendDone",
                                        SensorLabel = "Cylinder1.ExtendDone",
                                        Address = "MR102"
                                    }
                                }
                            }
                        },
                        new()
                        {
                            Id = "s2",
                            Number = 2,
                            Label = "Step2",
                            ExecAddress = "MR1002",
                            DoneAddress = "MR1003",
                            Kind = "macro",
                            MacroFlowId = "flow-macro-step"
                        }
                    },
                    Transitions = new List<Transition>
                    {
                        new() { Id = "t1", Label = "T1", Condition = "MR102", FromStepIds = new List<string> { "s1" }, ToStepIds = new List<string> { "s2" } }
                    }
                },
                // Origin Flow
                new()
                {
                    Id = "flow-origin",
                    Name = "OriginFlow",
                    Type = "origin",
                    Mode = "origin",
                    DiagramType = "Macro",
                    Diagram = new DiagramInfo { Id = "diag-origin", UnitId = "unit-1", Unit = "MainUnit", BaseMr = "2000", BoolAddressMode = "linear" },
                    Steps = new List<Step>
                    {
                        new()
                        {
                            Id = "s10",
                            Number = 10,
                            Label = "OriginStep",
                            IsInitial = true,
                            ExecAddress = "MR2000",
                            DoneAddress = "MR2001",
                            Actions = new List<StepAction>
                            {
                                new() { Variable = "Cylinder1.Extend", Qualifier = ActionQualifier.N }
                            }
                        }
                    },
                    Transitions = new List<Transition>()
                },
                // Macro Step Flow (called by Step 2 in AutoFlow)
                new()
                {
                    Id = "flow-macro-step",
                    Name = "SubFlow",
                    DiagramType = "MacroStep",
                    Diagram = new DiagramInfo { Id = "diag-macro-step", UnitId = "unit-1", Unit = "MainUnit", BaseMr = "3000", BoolAddressMode = "linear" },
                    Steps = new List<Step>
                    {
                        new()
                        {
                            Id = "s20",
                            Number = 20,
                            Label = "SubStep",
                            IsInitial = true,
                            ExecAddress = "MR3000",
                            DoneAddress = "MR3001"
                        }
                    },
                    Transitions = new List<Transition>()
                }
            }
        };
    }
}
