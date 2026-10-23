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
    public void TopologyAnalyzer_ClassifiesSequentialWaitAndJumpTransitions()
    {
        var analyzer = new TopologyAnalyzer();
        var flow = new FlowInfo
        {
            Name = "main",
            Steps = new List<Step>
            {
                new Step { Id = "s1", Number = 1, Label = "Step1" },
                new Step { Id = "s2", Number = 2, Label = "Step2" },
                new Step { Id = "s3", Number = 3, Label = "Step3" },
                new Step { Id = "s4", Number = 4, Label = "Step4" }
            },
            Transitions = new List<Transition>
            {
                new Transition { FromStepIds = new List<string> { "s1" }, ToStepIds = new List<string> { "s2" }, Condition = "diClamp = 1" },
                new Transition { FromStepIds = new List<string> { "s2" }, ToStepIds = new List<string> { "s4" }, Condition = "skip_mode = 1" }, // Forward jump > 1
                new Transition { FromStepIds = new List<string> { "s4" }, ToStepIds = new List<string> { "s1" }, Condition = "loop_again = 1" }  // Backward jump
            }
        };

        var ctx = analyzer.Analyze(flow);

        Assert.Contains(4, ctx.JumpTargetStepNumbers);
        Assert.Contains(1, ctx.JumpTargetStepNumbers);
        Assert.DoesNotContain(2, ctx.JumpTargetStepNumbers); // Step 2 is sequential 1-step, no label required

        var seqDetail = ctx.TransitionDetails.FirstOrDefault(t => t.FromStepNumber == 1);
        Assert.NotNull(seqDetail);
        Assert.Equal(TransitionKind.SequentialWait, seqDetail!.Kind);

        var jumpDetail = ctx.TransitionDetails.FirstOrDefault(t => t.FromStepNumber == 2);
        Assert.NotNull(jumpDetail);
        Assert.Equal(TransitionKind.ForwardJump, jumpDetail!.Kind);

        var bwDetail = ctx.TransitionDetails.FirstOrDefault(t => t.FromStepNumber == 4);
        Assert.NotNull(bwDetail);
        Assert.Equal(TransitionKind.BackwardJump, bwDetail!.Kind);
    }

    [Fact]
    public void AbbRapidGenerator_WithoutAiConfigured_GeneratesFailureFile()
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
            }
        };

        var files = generator.GenerateFiles(payload).ToList();

        Assert.Single(files);

        var modFile = files.FirstOrDefault(f => f.Path == "pick_place.mod");

        Assert.NotNull(modFile);
        Assert.Contains("ROBOT CODEGEN FAILED", modFile!.Content);
        Assert.Contains("AI_SERVICE_NOT_CONFIGURED", modFile.Content);
    }

    [Fact]
    public void TopologyAnalyzer_SelectionBranchAndMergePoint_PlansStructuredRapidWithoutGoto()
    {
        var flow = new FlowInfo
        {
            Name = "Drop_Work",
            Steps = new List<Step>
            {
                new Step { Id = "s1", Number = 1, Label = "Wait" },
                new Step { Id = "s2", Number = 2, Label = "Get" },
                new Step { Id = "s3", Number = 3, Label = "Wait" },
                new Step { Id = "s4", Number = 4, Label = "Put" },
                new Step { Id = "s6", Number = 6, Label = "Temp" }
            },
            Transitions = new List<Transition>
            {
                new Transition { Id = "t1a", FromStepIds = new List<string> { "s1" }, ToStepIds = new List<string> { "s2" }, Condition = "sig2" },
                new Transition { Id = "t1b", FromStepIds = new List<string> { "s1" }, ToStepIds = new List<string> { "s6" }, Condition = "Not sig2" },
                new Transition { Id = "t2",  FromStepIds = new List<string> { "s2" }, ToStepIds = new List<string> { "s3" }, Condition = "" },
                new Transition { Id = "t3",  FromStepIds = new List<string> { "s3" }, ToStepIds = new List<string> { "s4" }, Condition = "sig3" },
                new Transition { Id = "t6",  FromStepIds = new List<string> { "s6" }, ToStepIds = new List<string> { "s4" }, Condition = "sig3" },
                new Transition { Id = "t4",  FromStepIds = new List<string> { "s4" }, ToStepIds = new List<string> { "s1" }, Condition = "" }
            }
        };

        var analyzer = new TopologyAnalyzer();
        var topologyContext = analyzer.Analyze(flow);

        Assert.NotEmpty(topologyContext.ExecutionTree);

        var prompt = new RobotRapidPromptBuilder().BuildPrompts(flow, new List<DeviceVariable>(), topologyContext, "Drop_Work");
        var plan = prompt.UserPrompt.Split("=== STRUCTURE PLAN")[1];

        Assert.Contains("IF sig2 THEN", plan);
        Assert.Contains("ELSE", plan);
        Assert.Contains("ENDIF", plan);
        Assert.DoesNotContain("END_IF", plan);
        Assert.Contains("WaitDI sig3, 1;", prompt.RequiredFragments);
        // The branch converges structurally; the only jump is the loop back from step 4 to step 1.
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(plan, "GOTO"));
        Assert.Contains("GOTO lbl_step1;", plan);
    }

    [Fact]
    public void TopologyAnalyzer_SelectionBranchAllPathConvergence_ClassifiesTransitionAsConvergence()
    {
        var flow = new FlowInfo
        {
            Name = "Drop_Work",
            Steps = new List<Step>
            {
                new Step { Id = "s1", Number = 1, Label = "Wait" },
                new Step { Id = "s2", Number = 2, Label = "Get" },
                new Step { Id = "s3", Number = 3, Label = "Wait" },
                new Step { Id = "s4", Number = 4, Label = "Put" },
                new Step { Id = "s6", Number = 6, Label = "Temp" }
            },
            Transitions = new List<Transition>
            {
                new Transition { Id = "t1a", FromStepIds = new List<string> { "s2" }, ToStepIds = new List<string> { "s3" }, Condition = "sig2" },
                new Transition { Id = "t1b", FromStepIds = new List<string> { "s2" }, ToStepIds = new List<string> { "s6" }, Condition = "Not sig2" },
                new Transition { Id = "t3",  FromStepIds = new List<string> { "s3" }, ToStepIds = new List<string> { "s4" }, Condition = "sig3" },
                new Transition { Id = "t6",  FromStepIds = new List<string> { "s6" }, ToStepIds = new List<string> { "s4" }, Condition = "sig3" }
            }
        };

        var analyzer = new TopologyAnalyzer();
        var ctx = analyzer.Analyze(flow);

        var td6To4 = ctx.TransitionDetails.FirstOrDefault(t => t.FromStepNumber == 6 && t.ToStepNumber == 4);
        Assert.NotNull(td6To4);
        Assert.Equal(TransitionKind.Convergence, td6To4!.Kind);
        Assert.DoesNotContain(4, ctx.JumpTargetStepNumbers);
        Assert.DoesNotContain(ctx.BackwardTransitions, bt => bt.FromStepNumber == 6 && bt.ToStepNumber == 4);
    }

    [Fact]
    public void TopologyAnalyzer_FullDropWorkTopology_ClassifiesStep6ToStep4AsConvergence()
    {
        // Mirrors the EXACT Drop_Work flow as seen in the app DEBUG log:
        // [T6]  FROM=[S1]     TO=[S2] COND="sig1"
        // [T9]  FROM=[S2]     TO=[S3] COND="sig2"
        // [T10] FROM=[S3,S15] TO=[S4] COND="sig3"  ← single transition, 2 FromStepIds!
        // [T23] FROM=[S2]     TO=[S15] COND="Not sig2"
        // Step 6 has actual Id "S15" in the app
        var flow = new FlowInfo
        {
            Name = "Drop_Work",
            Steps = new List<Step>
            {
                new Step { Id = "S1",  Number = 1, Label = "Wait" },
                new Step { Id = "S2",  Number = 2, Label = "Get" },
                new Step { Id = "S3",  Number = 3, Label = "Wait" },
                new Step { Id = "S4",  Number = 4, Label = "Put" },
                new Step { Id = "S15", Number = 6, Label = "Temp" }
            },
            Transitions = new List<Transition>
            {
                // T6: S1 → S2 (sig1)
                new Transition { Id = "T6",  FromStepIds = new List<string> { "S1" },       ToStepIds = new List<string> { "S2" }, Condition = "sig1" },
                // T9: S2 → S3 (sig2)
                new Transition { Id = "T9",  FromStepIds = new List<string> { "S2" },       ToStepIds = new List<string> { "S3" }, Condition = "sig2" },
                // T10: S3+S15 → S4 (sig3) — shared merge transition with 2 FromStepIds
                new Transition { Id = "T10", FromStepIds = new List<string> { "S3", "S15" }, ToStepIds = new List<string> { "S4" }, Condition = "sig3" },
                // T23: S2 → S15 (Not sig2)
                new Transition { Id = "T23", FromStepIds = new List<string> { "S2" },       ToStepIds = new List<string> { "S15" }, Condition = "Not sig2" },
            }
        };

        var analyzer = new TopologyAnalyzer();
        var ctx = analyzer.Analyze(flow);

        // Step6 (S15) → Step4 (S4) must be CONVERGENCE, not BACKWARD JUMP
        var td6To4 = ctx.TransitionDetails.FirstOrDefault(t => t.FromStepNumber == 6 && t.ToStepNumber == 4);
        Assert.NotNull(td6To4);
        Assert.Equal(TransitionKind.Convergence, td6To4!.Kind);

        // Step 4 must NOT appear as a jump target (no GOTO lbl_step4)
        Assert.DoesNotContain(4, ctx.JumpTargetStepNumbers);

        // Step4 must NOT be a backward transition target from Step6
        Assert.DoesNotContain(ctx.BackwardTransitions, bt => bt.FromStepNumber == 6 && bt.ToStepNumber == 4);
    }

    [Fact]
    public void TopologyAnalyzer_PartialConvergence_DoesNotClassifyAsConvergence()
    {
        var flow = new FlowInfo
        {
            Name = "Partial_Branch",
            Steps = new List<Step>
            {
                new Step { Id = "s1", Number = 1, Label = "Init" },
                new Step { Id = "s2", Number = 2, Label = "Branch" },
                new Step { Id = "s3", Number = 3, Label = "NormalPath" },
                new Step { Id = "s4", Number = 4, Label = "SharedTarget" },
                new Step { Id = "s6", Number = 6, Label = "ErrorExitPath" }
            },
            Transitions = new List<Transition>
            {
                new Transition { FromStepIds = new List<string> { "s2" }, ToStepIds = new List<string> { "s3" }, Condition = "ok_mode" },
                new Transition { FromStepIds = new List<string> { "s2" }, ToStepIds = new List<string> { "s6" }, Condition = "err_mode" },
                new Transition { FromStepIds = new List<string> { "s3" }, ToStepIds = new List<string> { "s4" }, Condition = "sig3" },
                new Transition { FromStepIds = new List<string> { "s6" }, ToStepIds = new List<string> { "s1" }, Condition = "reset_err" } // Loops back to 1 instead of merging at 4
            }
        };

        var analyzer = new TopologyAnalyzer();
        var ctx = analyzer.Analyze(flow);

        var td6To1 = ctx.TransitionDetails.FirstOrDefault(t => t.FromStepNumber == 6 && t.ToStepNumber == 1);
        Assert.NotNull(td6To1);
        Assert.Equal(TransitionKind.BackwardJump, td6To1!.Kind);
        Assert.Contains(1, ctx.JumpTargetStepNumbers);
    }

    [Fact]
    public void RobotRapidPromptBuilder_FiltersUnusedVariablesAndOmitsQualifierN()
    {
        var builder = new RobotRapidPromptBuilder();
        var flow = new FlowInfo
        {
            Name = "Drop_Work",
            Steps = new List<Step>
            {
                new Step
                {
                    Id = "s1",
                    Number = 1,
                    Label = "Wait",
                    Actions = new List<StepAction>
                    {
                        new StepAction { Qualifier = GrafcetStudio.Domain.Enums.ActionQualifier.N, Variable = "Pos1", Address = "Pos1" }
                    }
                }
            },
            Transitions = new List<Transition>
            {
                new Transition { FromStepIds = new List<string> { "s1" }, ToStepIds = new List<string> { "s2" }, Condition = "sig1" }
            }
        };

        var variables = new List<DeviceVariable>
        {
            new DeviceVariable { Label = "Pos1", Format = "POS", Address = "P1" },
            new DeviceVariable { Label = "sig1", Format = "BOOL", Address = "DI1" },
            new DeviceVariable { Label = "unused_clamp", Format = "BOOL", Address = "DO99" }
        };

        var topologyContext = new TopologyAnalyzer().Analyze(flow);
        var userPrompt = builder.BuildPrompts(flow, variables, topologyContext, "Drop_Work").UserPrompt;

        Assert.Contains("Pos1 | POS | P1", userPrompt);
        Assert.Contains("sig1 | BOOL | DI1", userPrompt);
        Assert.DoesNotContain("unused_clamp", userPrompt);
        Assert.Contains("Variable: Pos1, Address: Pos1", userPrompt);
        Assert.DoesNotContain("Qualifier: N", userPrompt);
    }
}


