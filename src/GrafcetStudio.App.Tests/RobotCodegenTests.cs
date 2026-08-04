using System;
using System.Collections.Generic;
using System.Linq;
using GrafcetStudio.App.Generators.Robot;
using GrafcetStudio.Domain.Models;
using Xunit;

namespace GrafcetStudio.App.Tests;

public class RobotCodegenTests
{
    [Fact]
    public void RobotIrValidator_ValidJson_ReturnsSuccess()
    {
        var validator = new RobotIrValidator();
        var json = @"
{
  ""$schema"": ""grafectstudio/robot-ir/v1"",
  ""platform"": ""ABB_RAPID"",
  ""module"": ""MainModule"",
  ""signals"": [
    { ""name"": ""gripper_open"", ""type"": ""DO"", ""alias"": ""DO1"" },
    { ""name"": ""sensor_part"", ""type"": ""DI"", ""alias"": ""DI1"" }
  ],
  ""positions"": [
    { ""name"": ""pHome"", ""motionType"": ""AbsJ"", ""speed"": ""Fast"" },
    { ""name"": ""pPickup"", ""motionType"": ""Linear"", ""speed"": ""Precise"" }
  ],
  ""tools"": [
    { ""name"": ""tool1"" }
  ],
  ""init"": {
    ""instructions"": [
      { ""type"": ""SetDO"", ""signal"": ""gripper_open"", ""value"": 0 },
      { ""type"": ""MoveAbsJ"", ""target"": ""pHome"", ""tool"": ""tool1"" }
    ]
  },
  ""steps"": [
    {
      ""id"": 1,
      ""name"": ""Pick"",
      ""grafectStepId"": 1,
      ""instructions"": [
        { ""type"": ""WaitDI"", ""signal"": ""sensor_part"", ""value"": 1 },
        { ""type"": ""MoveL"", ""target"": ""pPickup"", ""tool"": ""tool1"" },
        { ""type"": ""SetDO"", ""signal"": ""gripper_open"", ""value"": 1 }
      ]
    }
  ]
}";

        var result = validator.Validate(json);

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
        Assert.NotNull(result.Document);
        Assert.Equal("ABB_RAPID", result.Document!.Platform);
    }

    [Fact]
    public void RobotIrValidator_InvalidInstructionType_ReturnsErrors()
    {
        var validator = new RobotIrValidator();
        var json = @"
{
  ""$schema"": ""grafectstudio/robot-ir/v1"",
  ""platform"": ""ABB_RAPID"",
  ""module"": ""MainModule"",
  ""steps"": [
    {
      ""id"": 1,
      ""name"": ""BadStep"",
      ""instructions"": [
        { ""type"": ""INVALID_DANCE_CMD"", ""target"": ""pHome"" }
      ]
    }
  ]
}";

        var result = validator.Validate(json);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("invalid type 'INVALID_DANCE_CMD'"));
    }

    [Fact]
    public void RobotIrValidator_UndeclaredPositionTarget_ReturnsErrors()
    {
        var validator = new RobotIrValidator();
        var json = @"
{
  ""$schema"": ""grafectstudio/robot-ir/v1"",
  ""platform"": ""ABB_RAPID"",
  ""module"": ""MainModule"",
  ""positions"": [
    { ""name"": ""pHome"", ""motionType"": ""AbsJ"" }
  ],
  ""steps"": [
    {
      ""id"": 1,
      ""name"": ""MoveStep"",
      ""instructions"": [
        { ""type"": ""MoveJ"", ""target"": ""pGhostPosition"", ""tool"": ""tool1"" }
      ]
    }
  ]
}";

        var result = validator.Validate(json);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("undeclared position 'pGhostPosition'"));
    }

    [Fact]
    public void RapidRenderer_RendersValidRapidModule()
    {
        var renderer = new RapidRenderer();
        var doc = new RobotIrDocument
        {
            Schema = "grafectstudio/robot-ir/v1",
            Platform = "ABB_RAPID",
            Module = "PickModule",
            Signals = new List<RobotSignal>
            {
                new RobotSignal { Name = "gripper", Type = "DO", Alias = "DO1" }
            },
            Positions = new List<RobotPosition>
            {
                new RobotPosition { Name = "pHome", MotionType = "AbsJ", Speed = "Fast" },
                new RobotPosition { Name = "pPick", MotionType = "Linear", Speed = "Precise" }
            },
            Tools = new List<RobotTool> { new RobotTool { Name = "tool1" } },
            Steps = new List<RobotStep>
            {
                new RobotStep
                {
                    Id = 1,
                    Name = "Pick Step",
                    Instructions = new List<RobotInstruction>
                    {
                        new RobotInstruction
                        {
                            Type = "MoveL",
                            Target = "pPick",
                            Tool = "tool1",
                            Offset = new RobotOffset { X = 0, Y = 0, Z = 100 }
                        },
                        new RobotInstruction
                        {
                            Type = "SetDO",
                            Signal = "gripper",
                            Value = 1
                        }
                    }
                }
            }
        };

        var rapidText = renderer.Render(doc);

        Assert.Contains("MODULE PickModule", rapidText);
        Assert.Contains("ALIAS DO gripper := DO1;", rapidText);
        Assert.Contains("PERS robtarget pHome", rapidText);
        Assert.Contains("PERS robtarget pPick", rapidText);
        Assert.Contains("Offs(pPick, 0, 0, 100)", rapidText);
        Assert.Contains("SetDO gripper, 1;", rapidText);
        Assert.Contains("ENDMODULE", rapidText);
    }

    [Fact]
    public void TeachListGenerator_GeneratesChecklistText()
    {
        var gen = new TeachListGenerator();
        var doc = new RobotIrDocument
        {
            Platform = "ABB_RAPID",
            Module = "PalletModule",
            Positions = new List<RobotPosition>
            {
                new RobotPosition { Name = "pHome", MotionType = "AbsJ", Description = "Home safety point" },
                new RobotPosition { Name = "pPalletBase", MotionType = "Joint", Description = "Base grid start" }
            },
            Tools = new List<RobotTool>
            {
                new RobotTool { Name = "tool1", Description = "Vacuum Gripper TCP" }
            }
        };

        var checklist = gen.GenerateTeachList(doc, "PalletModule");

        Assert.Contains("ROBOT POSITION TEACH CHECKLIST (ABB_RAPID)", checklist);
        Assert.Contains("Position Name : pHome", checklist);
        Assert.Contains("Position Name : pPalletBase", checklist);
        Assert.Contains("Tool Name     : tool1", checklist);
        Assert.Contains("Status        : [ ] TAUGHT ON FLEXPENDANT", checklist);
    }

    [Fact]
    public void AbbRapidGenerator_GeneratesFilesFromPayload()
    {
        var generator = new AbbRapidGenerator();
        var payload = new CodegenPayload
        {
            Platform = "ABB Robot",
            Flows = new List<FlowInfo>
            {
                new FlowInfo
                {
                    Name = "pick_place",
                    Steps = new List<Step>
                    {
                        new Step { Number = 1, Label = "Wait Sensor" },
                        new Step { Number = 2, Label = "Move to Pick" }
                    }
                }
            },
            Variables = new List<DeviceVariable>
            {
                new DeviceVariable { Label = "pHome", Format = "POS" },
                new DeviceVariable { Label = "pPick", Format = "POS" }
            }
        };

        var files = generator.GenerateFiles(payload).ToList();

        Assert.Equal(2, files.Count);

        var modFile = files.FirstOrDefault(f => f.Path == "pick_place.mod");
        var teachFile = files.FirstOrDefault(f => f.Path == "pick_place_teach.txt");

        Assert.NotNull(modFile);
        Assert.NotNull(teachFile);
        Assert.Contains("MODULE pick_place", modFile!.Content);
        Assert.Contains("ROBOT POSITION TEACH CHECKLIST", teachFile!.Content);
    }
}
