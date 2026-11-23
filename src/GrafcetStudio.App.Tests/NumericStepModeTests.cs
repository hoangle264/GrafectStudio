using GrafcetStudio.App.Generators;
using GrafcetStudio.App.Generators.Common;
using GrafcetStudio.CodeGen.Template;
using GrafcetStudio.Domain.Enums;
using GrafcetStudio.Domain.Models;
using GrafcetStudio.Domain.Resolution;
using HandlebarsDotNet;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace GrafcetStudio.App.Tests;

/// <summary>Numeric step mode: one step register per flow, CASE values 10/20/..., no Done bits.</summary>
public class NumericStepModeTests
{
    private static CodegenPayload BuildPayload() => new()
    {
        Project = new ProjectInfo { Name = "NumericDemo" },
        Unit = new UnitInfo { Id = "unit-1", Name = "MainUnit", Label = "MainUnit" },
        Variables = new List<DeviceVariable>
        {
            new()
            {
                Label = "MainUnit",
                Format = "Unit",
                SignalAddresses = new Dictionary<string, string> { ["flagAuto"] = "MR0", ["flagManual"] = "MR1", ["flagOrigin"] = "MR2" }
            },
            new()
            {
                Label = "Cylinder1",
                Format = "Cylinder",
                SignalAddresses = new Dictionary<string, string> { ["CoilA"] = "MR100", ["ExtendDone"] = "MR102" }
            }
        },
        DeviceTypes = new List<DeviceType>
        {
            new() { Name = "Cylinder", Signals = new List<DeviceSignal> { new() { Id = "coilA", Name = "CoilA" }, new() { Id = "extendDone", Name = "ExtendDone" } } }
        },
        Flows = new List<FlowInfo>
        {
            new()
            {
                Id = "flow-auto",
                Name = "AutoFlow",
                Type = "auto",
                Mode = "auto",
                DiagramType = "Macro",
                Diagram = new DiagramInfo { Id = "diag-auto", UnitId = "unit-1", Unit = "MainUnit", AddressMode = "numeric", ActiveWord = "DM0", ActiveWordTag = "StepNo" },
                Steps = new List<Step>
                {
                    new()
                    {
                        Id = "s1", Number = 1, StepValue = 10, Label = "Extend", IsInitial = true,
                        Actions = new List<StepAction> { new() { Variable = "Cylinder1.CoilA", Qualifier = ActionQualifier.N } }
                    },
                    new() { Id = "s2", Number = 2, StepValue = 20, Label = "Wait" },
                    new() { Id = "s3", Number = 3, StepValue = 35, Label = "Finish" }
                },
                Transitions = new List<Transition>
                {
                    new() { Id = "t1", Condition = "MR102", FromStepIds = new List<string> { "s1" }, ToStepIds = new List<string> { "s2" } },
                    new() { Id = "t2", Condition = "MR103", FromStepIds = new List<string> { "s2" }, ToStepIds = new List<string> { "s3" } },
                    new() { Id = "t3", Condition = "MR104", FromStepIds = new List<string> { "s3" }, ToStepIds = new List<string> { "s1" } }
                }
            }
        }
    };

    [Fact]
    public void Context_ExposesStepRegisterAndCaseValues()
    {
        var context = GeneratorContextBuilder.Build(BuildPayload(), new SequenceResolver());

        var flow = Assert.Single(context.autoFlows);
        Assert.True(flow.isNumericStep);
        Assert.Equal("StepNo", flow.stepVar);
        Assert.Equal(new[] { 10, 20, 35 }, flow.steps.Select(step => step.StepValue));
        // The last step follows its out-transition back to the first step, not "35 + 10".
        Assert.Equal(new[] { 20, 35, 10 }, flow.steps.Select(step => step.NextStepValue));
    }

    [Fact]
    public void Template_RendersCaseBlockFromNumericData()
    {
        var templates = new TemplateManager(Handlebars.Create());
        templates.LoadTemplate("uc.unitAuto",
            "{{#each autoFlows}}{{#if isNumericStep}}CASE {{stepVar}} OF\n{{#each steps}}{{StepValue}}: ; {{StepLabel}}\n{{#if OutTransition}}IF {{OutTransition.Condition}} THEN {{../stepVar}} := {{NextStepValue}}; END_IF;\n{{/if}}{{/each}}END_CASE;{{/if}}{{/each}}");
        templates.LoadTemplate("uc.unitOrigin", "");
        templates.LoadTemplate("uc.unitMainOutput", "");
        var generator = new KeyenceGenerator(templates, new SequenceResolver());

        var output = generator.GenerateUnitContent(BuildPayload()).Replace("\r\n", "\n");

        Assert.Contains("CASE StepNo OF", output);
        Assert.Contains("10: ; Extend\nIF MR102 THEN StepNo := 20; END_IF;", output);
        Assert.Contains("35: ; Finish\nIF MR104 THEN StepNo := 10; END_IF;", output);
        Assert.Contains("END_CASE;", output);
    }

    [Fact]
    public void DeviceCommand_SplitsNumericStepCondition()
    {
        var command = new GrafcetStudio.CodeGen.Runtime.Models.DeviceCommandFlowOutput { SourceExecuteBit = "StepNo=20" };
        Assert.True(command.IsNumericStep);
        Assert.Equal("StepNo", command.StepVar);
        Assert.Equal(20, command.StepValue);

        var bitCommand = new GrafcetStudio.CodeGen.Runtime.Models.DeviceCommandFlowOutput { SourceExecuteBit = "MR1000", SourceDoneBit = "MR1001" };
        Assert.False(bitCommand.IsNumericStep);
        Assert.Equal(string.Empty, bitCommand.StepVar);
    }
}
