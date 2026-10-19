using GrafcetStudio.App.Generators.Keyence;
using GrafcetStudio.Domain.Enums;
using GrafcetStudio.Domain.Models;
using Xunit;

namespace GrafcetStudio.App.Tests;

public class KeyenceInstructionModelTests
{
    [Theory]
    [InlineData(KeyenceInstructionType.Out, typeof(KeyenceCoilInstruction), "OUT  MR100")]
    [InlineData(KeyenceInstructionType.Set, typeof(KeyenceSetInstruction), "SET  MR100")]
    [InlineData(KeyenceInstructionType.Rst, typeof(KeyenceResetInstruction), "RST  MR100")]
    [InlineData(KeyenceInstructionType.Res, typeof(KeyenceResetByResInstruction), "RES  MR100")]
    [InlineData(KeyenceInstructionType.Fb, typeof(KeyenceFunctionBlockInstruction), "FB   MR100")]
    public void Create_OutputInstructionTypes_FormatsExpectedMnemonic(KeyenceInstructionType type, Type expectedModel, string expectedLine)
    {
        var instruction = KeyenceOutputInstruction.Create(type, "MR100");

        Assert.IsType(expectedModel, instruction);
        Assert.Equal(type, instruction.InstructionType);
        Assert.Equal(expectedLine, KeyenceMnemonicInstructionEmitter.Format(instruction));
    }

    [Theory]
    [InlineData(ActionQualifier.N, typeof(KeyenceCoilInstruction), "OUT  MR10")]
    [InlineData(ActionQualifier.S, typeof(KeyenceSetInstruction), "SET  MR10")]
    [InlineData(ActionQualifier.R, typeof(KeyenceResetInstruction), "RST  MR10")]
    public void FromAction_KnownQualifiers_MapsToOutputInstructions(ActionQualifier qualifier, Type expectedModel, string expectedLine)
    {
        var vars = new List<DeviceVariable>
        {
            new()
            {
                Label = "Motor",
                SignalAddresses = new Dictionary<string, string> { ["Run"] = "MR10" }
            }
        };
        var action = new StepAction { Variable = "Motor.Run", Qualifier = qualifier };

        var instruction = KeyenceOutputInstruction.FromAction(action, vars);

        Assert.IsType(expectedModel, instruction);
        Assert.Equal("MR10", instruction.TargetRef);
        Assert.Equal("Motor.Run", instruction.SourceRef);
        Assert.Equal(expectedLine, KeyenceMnemonicInstructionEmitter.Format(instruction));
    }

    [Fact]
    public void FromAction_UnsupportedQualifier_PreservesLegacyCommentForm()
    {
        var action = new StepAction { Address = "MR20", Variable = "Pulse", Qualifier = ActionQualifier.P };

        var instruction = KeyenceOutputInstruction.FromAction(action, Array.Empty<DeviceVariable>());

        var unsupported = Assert.IsType<KeyenceUnsupportedOutputInstruction>(instruction);
        Assert.Equal(ActionQualifier.P, unsupported.Qualifier);
        Assert.Equal("; [P] MR20 - not implemented", KeyenceMnemonicInstructionEmitter.Format(unsupported));
    }
}
