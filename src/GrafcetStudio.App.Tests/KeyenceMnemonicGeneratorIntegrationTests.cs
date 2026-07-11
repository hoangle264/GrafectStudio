using GrafcetStudio.App.Generators;
using GrafcetStudio.Domain.Models;
using Xunit;

namespace GrafcetStudio.App.Tests;

public class KeyenceMnemonicGeneratorIntegrationTests
{
    [Theory]
    [InlineData("", "")]
    [InlineData("1", "true")]
    [InlineData(" 1 ", " TRUE ")]
    public void GenerateLegacy_SkipsEmptyTrueAndOneConditions(string inCondition, string outCondition)
    {
        var generator = new KeyenceMnemonicGenerator();
        var payload = BuildPayload(inCondition, outCondition);

        var file = Assert.Single(generator.GenerateFiles(payload));

        Assert.Contains("LD   CR2002      ; S01 prev done", file.Content);
        Assert.DoesNotContain("AND  ", file.Content);
        Assert.DoesNotContain("ANL", file.Content);
        Assert.Contains("SET  @MR100      ; S01 exec", file.Content);
        Assert.Contains("LD   @MR100      ; S01 exec", file.Content);
        Assert.Contains("SET  @MR101      ; S01 done", file.Content);
    }

    [Fact]
    public void GenerateLegacy_SimpleCondition_PreservesLegacyAndShape()
    {
        var generator = new KeyenceMnemonicGenerator();
        var payload = BuildPayload(inCondition: "StartButton", outCondition: "Sensor.Done");

        var file = Assert.Single(generator.GenerateFiles(payload));

        Assert.Contains("LD   CR2002      ; S01 prev done" + Environment.NewLine
                      + "AND  StartButton" + Environment.NewLine
                      + "SET  @MR100      ; S01 exec", file.Content);

        Assert.Contains("LD   @MR100      ; S01 exec" + Environment.NewLine
                      + "AND  MR10" + Environment.NewLine
                      + "SET  @MR101      ; S01 done", file.Content);
    }

    [Fact]
    public void GenerateLegacy_GroupedCondition_UsesExpressionEmitterInAndWithCurrentMode()
    {
        var generator = new KeyenceMnemonicGenerator();
        var payload = BuildPayload(inCondition: "(A|B)&Sensor.Done", outCondition: "A&(B|C)");

        var file = Assert.Single(generator.GenerateFiles(payload));

        Assert.Contains("LD   CR2002      ; S01 prev done" + Environment.NewLine
                      + "LD   A" + Environment.NewLine
                      + "OR   B" + Environment.NewLine
                      + "ANL" + Environment.NewLine
                      + "AND  MR10" + Environment.NewLine
                      + "SET  @MR100      ; S01 exec", file.Content);

        Assert.Contains("LD   @MR100      ; S01 exec" + Environment.NewLine
                      + "AND  A" + Environment.NewLine
                      + "LD   B" + Environment.NewLine
                      + "OR   C" + Environment.NewLine
                      + "ANL" + Environment.NewLine
                      + "SET  @MR101      ; S01 done", file.Content);
    }

    private static CodegenPayload BuildPayload(string? inCondition, string? outCondition)
        => new()
        {
            Project = new ProjectInfo { Name = "Demo" },
            Variables = new List<DeviceVariable>
            {
                new()
                {
                    Label = "Sensor",
                    SignalAddresses = new Dictionary<string, string>
                    {
                        ["Done"] = "MR10"
                    }
                }
            },
            Flows = new List<FlowInfo>
            {
                new()
                {
                    Id = "flow-1",
                    Name = "MainFlow",
                    Steps = new List<Step>
                    {
                        new()
                        {
                            Id = "s1",
                            Number = 1,
                            IsInitial = true,
                            ExecAddress = "@MR100",
                            DoneAddress = "@MR101"
                        }
                    },
                    Transitions = new List<Transition>
                    {
                        new()
                        {
                            Id = "t-in",
                            Label = "Tin",
                            Condition = inCondition ?? string.Empty,
                            ToStepIds = new List<string> { "s1" }
                        },
                        new()
                        {
                            Id = "t-out",
                            Label = "Tout",
                            Condition = outCondition ?? string.Empty,
                            FromStepIds = new List<string> { "s1" }
                        }
                    }
                }
            }
        };
}
