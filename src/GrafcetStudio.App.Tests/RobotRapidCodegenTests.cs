using GrafcetStudio.App.Generators.Robot;
using GrafcetStudio.App.Services.Ai;
using GrafcetStudio.Domain.Models;
using Xunit;

namespace GrafcetStudio.App.Tests;

public class RobotRapidCodegenTests
{
    private static FlowInfo PickFlow() => new()
    {
        Name = "Pick",
        Steps = new List<Step>
        {
            new Step { Id = "s1", Number = 1, Label = "Home", IsInitial = true },
            new Step { Id = "s2", Number = 2, Label = "Pick" }
        },
        Transitions = new List<Transition>
        {
            new Transition { FromStepIds = new List<string> { "s1" }, ToStepIds = new List<string> { "s2" }, Condition = "diPart" },
            new Transition { FromStepIds = new List<string> { "s2" }, ToStepIds = new List<string> { "s1" }, Condition = "diDone" }
        }
    };

    private static List<DeviceVariable> Vars() => new()
    {
        new DeviceVariable { Label = "diPart", Format = "DI" },
        new DeviceVariable { Label = "diDone", Format = "DI" },
        new DeviceVariable { Label = "pHome", Format = "POS" }
    };

    private static RobotRapidPrompt BuildPrompt()
    {
        var flow = PickFlow();
        return new RobotRapidPromptBuilder().BuildPrompts(flow, Vars(), new TopologyAnalyzer().Analyze(flow), "Pick");
    }

    /// <summary>A module that follows the plan: every placeholder replaced by a move to pHome.</summary>
    private static string ValidModule(RobotRapidPrompt prompt)
    {
        var plan = prompt.UserPrompt.Split("=== STRUCTURE PLAN")[1].Split('\n').Skip(1)
            .TakeWhile(l => !l.StartsWith("Generate the complete module"))
            .Select(l => l.Contains(RobotRapidPromptBuilder.ActionsPlaceholderPrefix) ? "MoveJ pHome, v500, fine, tool0;" : l);
        return "MODULE Pick\n" +
               "    CONST robtarget pHome := [[0,0,0],[1,0,0,0],[0,0,0,0],[9E9,9E9,9E9,9E9,9E9,9E9]]; ! TEACH\n" +
               "    PROC main()\n        WHILE TRUE DO\n" + string.Join("\n", plan) + "\n        ENDWHILE\n    ENDPROC\nENDMODULE";
    }

    [Fact]
    public void Plan_RequiresWaitsAndBackwardJump()
    {
        var prompt = BuildPrompt();
        Assert.Contains("WaitDI diPart, 1;", prompt.RequiredFragments);
        Assert.Contains("GOTO lbl_step1;", prompt.RequiredFragments);
        Assert.Contains("lbl_step1:", prompt.RequiredFragments);
        Assert.DoesNotContain("END_IF", prompt.SystemPrompt.Replace("`END_IF` is invalid", string.Empty));
    }

    [Fact]
    public void Validator_AcceptsModuleFollowingPlan()
    {
        var prompt = BuildPrompt();
        var errors = RapidModuleValidator.Validate(ValidModule(prompt), "Pick", prompt.AllowedIdentifiers, prompt.RequiredFragments);
        Assert.Empty(errors);
    }

    [Fact]
    public void Validator_RejectsMissingWait_UnknownIdentifier_AndEndIf()
    {
        var prompt = BuildPrompt();
        var code = ValidModule(prompt)
            .Replace("WaitDI diPart, 1;", "SetDO doGhost, 1;")
            .Replace("ENDWHILE", "IF diDone = 1 THEN\nStop;\nEND_IF\n        ENDWHILE");

        var errors = RapidModuleValidator.Validate(code, "Pick", prompt.AllowedIdentifiers, prompt.RequiredFragments);

        Assert.Contains(errors, e => e.Contains("WaitDI diPart, 1;"));
        Assert.Contains(errors, e => e.Contains("'doGhost'"));
        Assert.Contains(errors, e => e.Contains("END_IF is not RAPID syntax"));
    }

    [Fact]
    public void Validator_IgnoresRecordFieldsOptionalArgsAndComments()
    {
        var code = "MODULE M\n  PROC main()\n    ! uses ghostInComment\n    TPWrite \"ghostInString\";\n" +
                   "    MoveL Offs(pHome, 0, 0, 10), v100, fine, tool0\\WObj:=wobj0;\n    WaitUntil pHome.trans.z > 0 \\MaxTime:=5;\n  ENDPROC\nENDMODULE";
        var errors = RapidModuleValidator.Validate(code, "M", new[] { "pHome" }, Array.Empty<string>());
        Assert.Empty(errors);
    }

    [Fact]
    public void ExtractModule_StripsMarkdownFences()
    {
        Assert.Equal("MODULE A\nENDMODULE", RapidModuleValidator.ExtractModule("```rapid\nMODULE A\nENDMODULE\n```"));
    }

    [Fact]
    public void Generator_RepairsInvalidModule_ThenOutputsAiCode()
    {
        var good = ValidModule(BuildPrompt());
        var ai = new ScriptedAi(good.Replace("ENDWHILE", "END_IF\n ENDWHILE"), good);
        var files = new AbbRapidGenerator(ai).GenerateFiles(new CodegenPayload { Flows = new List<FlowInfo> { PickFlow() }, Variables = Vars() }).ToList();

        Assert.Equal(2, ai.Calls);
        Assert.Contains("VALIDATION ERRORS", ai.LastUserPrompt);
        var mod = files.Single(f => f.Path == "Pick.mod");
        Assert.DoesNotContain("CODEGEN FAILED", mod.Content);
        Assert.Contains("GOTO lbl_step1;", mod.Content);
        Assert.DoesNotContain(files, f => f.Path.EndsWith("_teach.txt"));
    }

    [Fact]
    public void Generator_FailsClosedAfterRepairBudget()
    {
        var ai = new ScriptedAi("MODULE Pick\nENDMODULE");
        var files = new AbbRapidGenerator(ai).GenerateFiles(new CodegenPayload { Flows = new List<FlowInfo> { PickFlow() }, Variables = Vars() }).ToList();

        Assert.Equal(3, ai.Calls);
        Assert.Contains("DO NOT LOAD", files.Single(f => f.Path == "Pick.mod").Content);
    }

    private sealed class ScriptedAi : IAiCompletionService
    {
        private readonly string[] _responses;
        public int Calls { get; private set; }
        public string LastUserPrompt { get; private set; } = string.Empty;

        public ScriptedAi(params string[] responses) => _responses = responses;

        public Task<AiCompletionResult> CompleteAsync(AiCompletionRequest request, CancellationToken cancellationToken = default)
        {
            LastUserPrompt = request.Request.Message;
            var text = _responses[Math.Min(Calls, _responses.Length - 1)];
            Calls++;
            return Task.FromResult(AiCompletionResult.Success(text));
        }
    }
}
