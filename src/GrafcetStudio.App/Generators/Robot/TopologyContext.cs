using System.Collections.Generic;
using GrafcetStudio.Domain.Models;

namespace GrafcetStudio.App.Generators.Robot;

public enum TransitionKind
{
    SequentialWait, // 1 step forward (Step N -> Step N+1): Use Wait...
    ForwardJump,    // Jump > 1 step forward (Step N -> Step N+K, K > 1): Use IF + GOTO
    BackwardJump,   // Jump backward (Step N -> Step M, M <= N): Use IF + GOTO
    SelectionBranch,// Alternative split: Use IF / ELSIF / TEST
    Convergence     // Branch convergence to merge point: Close IF/ELSIF/TEST before merge step, DO NOT USE GOTO!
}

public record TopologyContext
{
    public Dictionary<int, string> MacroCallMap { get; init; } = new();

    // Step numbers that are targets of GOTO jumps (backward jump)
    public HashSet<int> JumpTargetStepNumbers { get; init; } = new();

    // Backward compatibility alias
    public HashSet<int> BackwardTargetStepNumbers => JumpTargetStepNumbers;

    public List<BackwardTransitionInfo> BackwardTransitions { get; init; } = new();
    public Dictionary<int, SelectionBranchInfo> SelectionBranchMap { get; init; } = new();
    public List<TransitionDetailInfo> TransitionDetails { get; init; } = new();
    public List<ExecutionBlock> ExecutionTree { get; init; } = new();
}

public abstract record ExecutionBlock;

public record StepExecutionBlock(
    Step Step,
    string? WaitCondition
) : ExecutionBlock;

public record BranchArmExecutionPath(
    string Condition,
    int StartStepNumber,
    List<StepExecutionBlock> ArmStepBlocks,
    bool IsEarlyExit
);

public record SelectionBranchExecutionBlock(
    int FromStepNumber,
    List<BranchArmExecutionPath> Arms,
    bool IsIntegerVariablePattern,
    string? MergeCondition,
    int? ConvergentStepNumber
) : ExecutionBlock;

public record LoopBackExecutionBlock(
    int FromStepNumber,
    int ToStepNumber,
    string Condition
) : ExecutionBlock;

public record BackwardTransitionInfo(
    int FromStepNumber,
    int ToStepNumber,
    string Condition
);

public record TransitionDetailInfo(
    int FromStepNumber,
    int ToStepNumber,
    string Condition,
    TransitionKind Kind
);

public record SelectionBranchInfo(
    int FromStepNumber,
    List<SelectionBranchArm> Arms,
    bool IsIntegerVariablePattern
);

public record SelectionBranchArm(
    string Condition,
    int ToStepNumber
);

