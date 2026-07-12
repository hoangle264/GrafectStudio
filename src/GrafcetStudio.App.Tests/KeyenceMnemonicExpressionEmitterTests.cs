using GrafcetStudio.App.Generators.Keyence;
using GrafcetStudio.Domain.Models;
using Xunit;

namespace GrafcetStudio.App.Tests;

public class KeyenceMnemonicExpressionEmitterTests
{
    [Theory]
    [InlineData("A", new[] { "LD   A" })]
    [InlineData("A&B", new[] { "LD   A", "AND  B" })]
    [InlineData("A|B", new[] { "LD   A", "OR   B" })]
    [InlineData("(A|B)&C", new[] { "LD   A", "OR   B", "AND  C" })]
    [InlineData("A&(B|C)", new[] { "LD   A", "LD   B", "OR   C", "ANL" })]
    [InlineData("((A|B)&C)|D", new[] { "LD   A", "OR   B", "AND  C", "OR   D" })]
    [InlineData("(A&B)|(C&D)", new[] { "LD   A", "AND  B", "LD   C", "AND  D", "ORL" })]
    [InlineData("!A", new[] { "LDB  A" })]
    [InlineData("A&!B", new[] { "LD   A", "ANB  B" })]
    public void EmitCondition_BasicExpressions_ReturnsExpectedMnemonic(string expression, string[] expected)
    {
        var lines = KeyenceMnemonicExpressionEmitter.EmitCondition(expression, Array.Empty<DeviceVariable>());

        Assert.Equal(expected, lines);
    }

    [Fact]
    public void EmitCondition_ResolvesOperandsThroughDeviceVariables()
    {
        var vars = new List<DeviceVariable>
        {
            new()
            {
                Label = "Motor",
                SignalAddresses = new Dictionary<string, string> { ["Run"] = "MR10" }
            }
        };

        var lines = KeyenceMnemonicExpressionEmitter.EmitCondition("Motor.Run & @MR20", vars);

        Assert.Equal(new[] { "LD   MR10", "AND  @MR20" }, lines);
    }

    [Fact]
    public void EmitCondition_AndWithCurrentMode_UsesAndForSimpleOperand()
    {
        var lines = KeyenceMnemonicExpressionEmitter.EmitCondition(
            "StartButton",
            Array.Empty<DeviceVariable>(),
            new KeyenceExpressionEmitOptions { Mode = KeyenceExpressionEmitMode.AndWithCurrent, PadOperands = true });

        Assert.Equal(new[] { "AND  StartButton " }, lines);
    }

    [Fact]
    public void EmitCondition_AndWithCurrentMode_CombinesGroupedExpressionWithAnl()
    {
        var lines = KeyenceMnemonicExpressionEmitter.EmitCondition(
            "A|B",
            Array.Empty<DeviceVariable>(),
            new KeyenceExpressionEmitOptions { Mode = KeyenceExpressionEmitMode.AndWithCurrent });

        Assert.Equal(new[] { "LD   A", "OR   B", "ANL" }, lines);
    }

    [Fact]
    public void AppendExpressionAndInstruction_AppendsFinalOutputInstruction()
    {
        var sb = new System.Text.StringBuilder();
        var instruction = KeyenceOutputInstruction.Create(KeyenceInstructionType.Set, "MR100");

        KeyenceMnemonicExpressionEmitter.AppendExpressionAndInstruction(
            sb,
            "A&B",
            Array.Empty<DeviceVariable>(),
            instruction);

        Assert.Equal("LD   A" + Environment.NewLine
                     + "AND  B" + Environment.NewLine
                     + "SET  MR100" + Environment.NewLine,
            sb.ToString());
    }

    [Theory]
    [InlineData("A|B", KeyenceInstructionType.Rst, "RST  MR200")]
    [InlineData("(A|B)&C", KeyenceInstructionType.Out, "OUT  MR200")]
    [InlineData("A&(B|C)", KeyenceInstructionType.Fb, "FB   MR200")]
    public void EmitExpressionAndInstruction_SupportsOutputInstructionTypes(
        string expression,
        KeyenceInstructionType instructionType,
        string expectedInstructionLine)
    {
        var instruction = KeyenceOutputInstruction.Create(instructionType, "MR200");

        var text = KeyenceMnemonicExpressionEmitter.EmitExpressionAndInstruction(
            expression,
            Array.Empty<DeviceVariable>(),
            instruction);

        Assert.Contains(expectedInstructionLine, text);
        Assert.StartsWith("LD   ", text);
    }

    [Fact]
    public void CollectRefs_ReturnsExpressionOperands()
    {
        var expression = KeyenceMnemonicExpressionEmitter.Parse("(A|B)&Motor.Run");

        var refs = KeyenceMnemonicExpressionEmitter.CollectRefs(expression);

        Assert.Equal(new[] { "A", "B", "Motor.Run" }, refs.OrderBy(r => r, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void Parse_InvalidExpression_ThrowsHelpfulKeyenceError()
    {
        var ex = Assert.Throws<GrafcetStudio.App.Expressions.LogicExpressionParseException>(
            () => KeyenceMnemonicExpressionEmitter.Parse("A(B)"));

        Assert.Contains("Invalid Keyence mnemonic expression", ex.Message);
        Assert.Contains("position 1", ex.Message);
    }
}
