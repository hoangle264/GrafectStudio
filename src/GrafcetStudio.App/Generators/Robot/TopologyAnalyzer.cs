using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using GrafcetStudio.Domain.Models;

namespace GrafcetStudio.App.Generators.Robot;

public class TopologyAnalyzer
{
    private static readonly Regex IntPatternRegex = new(@"^\s*([a-zA-Z_][a-zA-Z0-9_]*)\s*=\s*(\d+)\s*$", RegexOptions.Compiled);

    public TopologyContext Analyze(FlowInfo flow, IReadOnlyDictionary<string, FlowInfo>? flowById = null)
    {
        var macroCallMap = new Dictionary<int, string>();
        var jumpTargets = new HashSet<int>();
        var backwardTransitions = new List<BackwardTransitionInfo>();
        var selectionBranchMap = new Dictionary<int, SelectionBranchInfo>();
        var transitionDetails = new List<TransitionDetailInfo>();
        var executionTree = new List<ExecutionBlock>();

        if (flow == null || flow.Steps == null || flow.Steps.Count == 0)
        {
            return new TopologyContext();
        }

        var steps = flow.Steps.OrderBy(s => s.Number).ToList();
        var stepMap = steps.ToDictionary(s => s.Id, s => s);
        var stepByNum = steps.ToDictionary(s => s.Number, s => s);

        // 1. Macro Call Map
        foreach (var step in steps)
        {
            if (string.Equals(step.Kind, "macro", StringComparison.OrdinalIgnoreCase))
            {
                string targetMacro;
                if (!string.IsNullOrWhiteSpace(step.MacroFlowId)
                    && flowById != null
                    && flowById.TryGetValue(step.MacroFlowId, out var macroFlow)
                    && !string.IsNullOrWhiteSpace(macroFlow.Name))
                {
                    targetMacro = macroFlow.Name;
                }
                else if (!string.IsNullOrWhiteSpace(step.MacroFlowId))
                {
                    targetMacro = step.MacroFlowId;
                }
                else
                {
                    targetMacro = "SubFlow";
                }
                macroCallMap[step.Number] = targetMacro;
            }
        }

        // 2. Group Outgoing and Incoming Transitions
        var outgoingTransitions = new Dictionary<string, List<Transition>>();
        var incomingTransitions = new Dictionary<string, List<Transition>>();

        foreach (var trans in flow.Transitions ?? new List<Transition>())
        {
            foreach (var fromId in trans.FromStepIds ?? new List<string>())
            {
                if (!outgoingTransitions.TryGetValue(fromId, out var list))
                {
                    list = new List<Transition>();
                    outgoingTransitions[fromId] = list;
                }
                list.Add(trans);
            }

            foreach (var toId in trans.ToStepIds ?? new List<string>())
            {
                if (!incomingTransitions.TryGetValue(toId, out var list))
                {
                    list = new List<Transition>();
                    incomingTransitions[toId] = list;
                }
                list.Add(trans);
            }
        }

        // Helper: a step is a merge point if multiple distinct source steps flow into it
        // Handles both: multiple separate Transition objects AND a single Transition with multiple FromStepIds
        bool IsMergePoint(string stepId)
        {
            if (!incomingTransitions.TryGetValue(stepId, out var inList)) return false;
            var distinctSources = inList.SelectMany(t => t.FromStepIds ?? new List<string>()).Distinct().Count();
            return distinctSources > 1;
        }

        // 3. Classify Transitions & Detect Jump Targets
        foreach (var (fromStepId, transList) in outgoingTransitions)
        {
            if (!stepMap.TryGetValue(fromStepId, out var fromStep)) continue;

            bool isSelectionBranch = transList.Count > 1;

            if (isSelectionBranch)
            {
                var arms = new List<SelectionBranchArm>();
                string? commonVarName = null;
                bool isIntegerPattern = true;

                foreach (var trans in transList)
                {
                    var targetToId = trans.ToStepIds?.FirstOrDefault();
                    int toStepNum = targetToId != null && stepMap.TryGetValue(targetToId, out var targetStep)
                        ? targetStep.Number
                        : 0;

                    var cond = trans.Condition ?? string.Empty;
                    arms.Add(new SelectionBranchArm(cond, toStepNum));

                    if (toStepNum != 0)
                    {
                        transitionDetails.Add(new TransitionDetailInfo(
                            fromStep.Number,
                            toStepNum,
                            cond,
                            TransitionKind.SelectionBranch
                        ));
                    }

                    var match = IntPatternRegex.Match(cond);
                    if (match.Success)
                    {
                        var varName = match.Groups[1].Value;
                        if (commonVarName == null)
                        {
                            commonVarName = varName;
                        }
                        else if (!string.Equals(commonVarName, varName, StringComparison.OrdinalIgnoreCase))
                        {
                            isIntegerPattern = false;
                        }
                    }
                    else
                    {
                        isIntegerPattern = false;
                    }
                }

                selectionBranchMap[fromStep.Number] = new SelectionBranchInfo(
                    fromStep.Number,
                    arms,
                    isIntegerPattern
                );
            }
            else
            {
                var trans = transList[0];
                foreach (var toId in trans.ToStepIds ?? new List<string>())
                {
                    if (stepMap.TryGetValue(toId, out var toStep))
                    {
                        int fromIndex = steps.IndexOf(fromStep);
                        int toIndex = steps.IndexOf(toStep);
                        if (toIndex == fromIndex + 1)
                        {
                            transitionDetails.Add(new TransitionDetailInfo(
                                fromStep.Number,
                                toStep.Number,
                                trans.Condition,
                                TransitionKind.SequentialWait
                            ));
                        }
                        else if (toIndex > fromIndex + 1)
                        {
                            transitionDetails.Add(new TransitionDetailInfo(
                                fromStep.Number,
                                toStep.Number,
                                trans.Condition,
                                TransitionKind.ForwardJump
                            ));
                        }
                        else
                        {
                            jumpTargets.Add(toStep.Number);
                            backwardTransitions.Add(new BackwardTransitionInfo(
                                fromStep.Number,
                                toStep.Number,
                                trans.Condition
                            ));
                            transitionDetails.Add(new TransitionDetailInfo(
                                fromStep.Number,
                                toStep.Number,
                                trans.Condition,
                                TransitionKind.BackwardJump
                            ));
                        }
                    }
                }
            }
        }

        // 4. Build Structured Execution Tree
        var visitedStepNumbers = new HashSet<int>();
        int currentStepIndex = 0;

        while (currentStepIndex < steps.Count)
        {
            var step = steps[currentStepIndex];

            if (visitedStepNumbers.Contains(step.Number))
            {
                currentStepIndex++;
                continue;
            }

            visitedStepNumbers.Add(step.Number);

            if (outgoingTransitions.TryGetValue(step.Id, out var transList) && transList.Count > 1)
            {
                // Selection Branch at this step
                var branchInfo = selectionBranchMap[step.Number];
                var armPaths = new List<BranchArmExecutionPath>();

                string? commonMergeCondition = null;
                int? commonMergeStepNumber = null;
                bool allArmsConverge = true;

                foreach (var trans in transList)
                {
                    var targetToId = trans.ToStepIds?.FirstOrDefault();
                    if (targetToId == null || !stepMap.TryGetValue(targetToId, out var targetStep)) continue;

                    var armBlocks = new List<StepExecutionBlock>();
                    var currArmStep = targetStep;
                    string? armMergeCond = null;
                    int? armMergeStepNum = null;
                    bool isEarlyExit = false;

                    while (currArmStep != null && !visitedStepNumbers.Contains(currArmStep.Number))
                    {
                        // Check if this step is a convergence point (has incoming transitions from other sources)
                        if (IsMergePoint(currArmStep.Id))
                        {
                            // Merge point reached!
                            armMergeStepNum = currArmStep.Number;

                            // Find transition leading into this merge point
                            var incomingList = incomingTransitions.GetValueOrDefault(currArmStep.Id);
                            var inTrans = incomingList?.FirstOrDefault(t => t.ToStepIds != null && t.ToStepIds.Contains(currArmStep.Id));
                            armMergeCond = inTrans?.Condition;
                            break;
                        }

                        visitedStepNumbers.Add(currArmStep.Number);

                        var currOutgoing = outgoingTransitions.GetValueOrDefault(currArmStep.Id);
                        string? nextWaitCond = null;
                        Step? nextStep = null;

                        if (currOutgoing != null && currOutgoing.Count == 1)
                        {
                            var outTrans = currOutgoing[0];
                            nextWaitCond = outTrans.Condition;

                            var nextToId = outTrans.ToStepIds?.FirstOrDefault();
                            if (nextToId != null && stepMap.TryGetValue(nextToId, out var foundNext))
                            {
                                if (foundNext.Number <= currArmStep.Number)
                                {
                                    // Check if this is actually a convergence merge point (not a loop back)
                                    if (IsMergePoint(foundNext.Id))
                                    {
                                        // Merge point (convergence), even though step number is lower
                                        armMergeStepNum = foundNext.Number;
                                        armMergeCond = outTrans.Condition;
                                        armBlocks.Add(new StepExecutionBlock(currArmStep, null));
                                        break;
                                    }
                                    else
                                    {
                                        // Genuine loop back inside arm
                                        jumpTargets.Add(foundNext.Number);
                                        isEarlyExit = true;
                                    }
                                }
                                else if (IsMergePoint(foundNext.Id))
                                {
                                    // Merge point step is foundNext
                                    armMergeStepNum = foundNext.Number;
                                    armMergeCond = outTrans.Condition;
                                    armBlocks.Add(new StepExecutionBlock(currArmStep, null));
                                    break;
                                }
                                else
                                {
                                    nextStep = foundNext;
                                }
                            }
                        }

                        armBlocks.Add(new StepExecutionBlock(currArmStep, nextWaitCond));
                        if (isEarlyExit) break;
                        currArmStep = nextStep;
                    }

                    armPaths.Add(new BranchArmExecutionPath(
                        trans.Condition ?? string.Empty,
                        targetStep.Number,
                        armBlocks,
                        isEarlyExit
                    ));

                    if (armMergeStepNum.HasValue)
                    {
                        if (commonMergeStepNumber == null)
                        {
                            commonMergeStepNumber = armMergeStepNum;
                            commonMergeCondition = armMergeCond;
                        }
                        else if (commonMergeStepNumber != armMergeStepNum)
                        {
                            allArmsConverge = false;
                        }
                    }
                    else if (!isEarlyExit)
                    {
                        allArmsConverge = false;
                    }
                }

                // Check outgoing transition from step before branch if any
                var stepWaitCond = transList.FirstOrDefault()?.Condition;
                if (transList.Count > 1 && string.Equals(transList[0].Condition, transList[1].Condition, StringComparison.OrdinalIgnoreCase))
                {
                    // same condition
                }

                // First step block before branch
                executionTree.Add(new StepExecutionBlock(step, null));

                executionTree.Add(new SelectionBranchExecutionBlock(
                    step.Number,
                    armPaths,
                    branchInfo.IsIntegerVariablePattern,
                    allArmsConverge ? commonMergeCondition : null,
                    allArmsConverge ? commonMergeStepNumber : null
                ));

                if (allArmsConverge && commonMergeStepNumber.HasValue && stepByNum.TryGetValue(commonMergeStepNumber.Value, out var mergeStep))
                {
                    // Move currentStepIndex to mergeStep
                    currentStepIndex = steps.IndexOf(mergeStep);
                }
                else
                {
                    currentStepIndex++;
                }
            }
            else
            {
                // Sequential step
                var currOutgoing = outgoingTransitions.GetValueOrDefault(step.Id);
                string? waitCond = null;

                if (currOutgoing != null && currOutgoing.Count == 1)
                {
                    var outTrans = currOutgoing[0];
                    waitCond = outTrans.Condition;

                    var toId = outTrans.ToStepIds?.FirstOrDefault();
                    if (toId != null && stepMap.TryGetValue(toId, out var toStep))
                    {
                        if (steps.IndexOf(toStep) <= steps.IndexOf(step))
                        {
                            // Loop back (Backward Jump)
                            executionTree.Add(new StepExecutionBlock(step, waitCond));
                            executionTree.Add(new LoopBackExecutionBlock(step.Number, toStep.Number, waitCond));
                            currentStepIndex++;
                            continue;
                        }
                        else if (steps.IndexOf(toStep) > steps.IndexOf(step) + 1)
                        {
                            // True Forward Jump
                            executionTree.Add(new StepExecutionBlock(step, waitCond));
                            executionTree.Add(new LoopBackExecutionBlock(step.Number, toStep.Number, waitCond));
                            currentStepIndex++;
                            continue;
                        }
                    }
                }

                executionTree.Add(new StepExecutionBlock(step, waitCond));
                currentStepIndex++;
            }
        }

        // 5. Post-process Selection Branch Convergence Transitions
        void ProcessExecutionBlocks(IEnumerable<ExecutionBlock> blocks)
        {
            foreach (var block in blocks)
            {
                if (block is SelectionBranchExecutionBlock branchBlock)
                {
                    if (branchBlock.ConvergentStepNumber.HasValue)
                    {
                        int mergeNum = branchBlock.ConvergentStepNumber.Value;

                        // Collect step numbers in arms of this selection branch
                        var armStepNums = new HashSet<int>();
                        foreach (var arm in branchBlock.Arms)
                        {
                            foreach (var sBlock in arm.ArmStepBlocks)
                            {
                                armStepNums.Add(sBlock.Step.Number);
                            }
                        }

                        // Any transition from an arm step leading into mergeNum is a Convergence transition
                        for (int i = 0; i < transitionDetails.Count; i++)
                        {
                            var td = transitionDetails[i];
                            if (armStepNums.Contains(td.FromStepNumber) && td.ToStepNumber == mergeNum)
                            {
                                transitionDetails[i] = new TransitionDetailInfo(
                                    td.FromStepNumber,
                                    td.ToStepNumber,
                                    td.Condition,
                                    TransitionKind.Convergence
                                );
                            }
                        }
                    }
                }
            }
        }

        ProcessExecutionBlocks(executionTree);

        // Rebuild jumpTargets and backwardTransitions based on updated transitionDetails and executionTree
        jumpTargets.Clear();
        backwardTransitions.Clear();

        foreach (var td in transitionDetails)
        {
            if (td.Kind == TransitionKind.BackwardJump || td.Kind == TransitionKind.ForwardJump)
            {
                jumpTargets.Add(td.ToStepNumber);
            }
            if (td.Kind == TransitionKind.BackwardJump || (td.Kind == TransitionKind.SelectionBranch && td.ToStepNumber <= td.FromStepNumber))
            {
                backwardTransitions.Add(new BackwardTransitionInfo(
                    td.FromStepNumber,
                    td.ToStepNumber,
                    td.Condition
                ));
            }
        }
        
        // Ensure all auto-injected GOTOs have a matching label
        foreach (var block in executionTree)
        {
            if (block is LoopBackExecutionBlock loopBlock)
            {
                jumpTargets.Add(loopBlock.ToStepNumber);
            }
            else if (block is SelectionBranchExecutionBlock branchBlock)
            {
                foreach (var arm in branchBlock.Arms)
                {
                    if (arm.ArmStepBlocks.Count == 0)
                    {
                        jumpTargets.Add(arm.StartStepNumber);
                    }
                }
            }
        }

        return new TopologyContext
        {
            MacroCallMap = macroCallMap,
            JumpTargetStepNumbers = jumpTargets,
            BackwardTransitions = backwardTransitions,
            SelectionBranchMap = selectionBranchMap,
            TransitionDetails = transitionDetails,
            ExecutionTree = executionTree
        };
    }

    public static string FormatRapidWaitInstruction(string? condition)
    {
        if (string.IsNullOrWhiteSpace(condition) || string.Equals(condition, "always", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var cond = condition.Trim();

        if (Regex.IsMatch(cond, @"^t#?(\d+(?:\.\d+)?)(ms|s)?$", RegexOptions.IgnoreCase))
        {
            var match = Regex.Match(cond, @"^t#?(\d+(?:\.\d+)?)(ms|s)?$", RegexOptions.IgnoreCase);
            double val = double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            if (string.Equals(match.Groups[2].Value, "ms", StringComparison.OrdinalIgnoreCase))
            {
                val /= 1000.0;
            }
            return $"WaitTime {val.ToString(System.Globalization.CultureInfo.InvariantCulture)};";
        }

        if (Regex.IsMatch(cond, @"^[a-zA-Z_][a-zA-Z0-9_]*$"))
        {
            return $"WaitDI {cond}, 1;";
        }

        var notMatch = Regex.Match(cond, @"^(?:not|!)\s*([a-zA-Z_][a-zA-Z0-9_]*)$", RegexOptions.IgnoreCase);
        if (notMatch.Success)
        {
            return $"WaitDI {notMatch.Groups[1].Value}, 0;";
        }

        var zeroMatch = Regex.Match(cond, @"^([a-zA-Z_][a-zA-Z0-9_]*)\s*=\s*0$", RegexOptions.IgnoreCase);
        if (zeroMatch.Success)
        {
            return $"WaitDI {zeroMatch.Groups[1].Value}, 0;";
        }

        var oneMatch = Regex.Match(cond, @"^([a-zA-Z_][a-zA-Z0-9_]*)\s*=\s*1$", RegexOptions.IgnoreCase);
        if (oneMatch.Success)
        {
            return $"WaitDI {oneMatch.Groups[1].Value}, 1;";
        }

        return $"WaitUntil {cond};";
    }
}

