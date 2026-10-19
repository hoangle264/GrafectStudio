using System;
using System.Collections.Generic;
using System.Linq;
using GrafcetStudio.Domain.Models;

namespace GrafcetStudio.App.Generators.Robot.RapidAst;

public record LintError(
    int StepNumber,
    string CodeSnippet,
    string ErrorMessage
);

public class RapidAstLinter
{
    private static readonly HashSet<string> BuiltInKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "SetDO", "WaitDI", "WaitDO", "WaitUntil", "WaitSyncTask", "SetAO", "WaitAI", "MoveJ", "MoveL", "MoveAbsJ", "WaitTime", "Reset", "Set",
        "v10", "v50", "v100", "v500", "v1000", "v2000", "vmax",
        "fine", "z0", "z1", "z5", "z10", "z50", "z100",
        "tool0", "wobj0", "TRUE", "FALSE", "high", "low"
    };

    public List<LintError> Lint(
        SnippetMapDocument doc,
        IList<DeviceVariable> variables,
        TopologyContext topologyContext)
    {
        var errors = new List<LintError>();
        if (doc == null || doc.Steps == null) return errors;

        // Build declared identifiers set
        var allowedVars = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (variables != null)
        {
            foreach (var v in variables)
            {
                if (!string.IsNullOrWhiteSpace(v.Label)) allowedVars.Add(v.Label);
            }
        }

        var declaredSignals = new HashSet<string>(doc.Signals?.Select(s => s.Name) ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        var declaredPositions = new HashSet<string>(doc.Positions?.Select(p => p.Name) ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        var declaredTools = new HashSet<string>(doc.Tools?.Select(t => t.Name) ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);

        foreach (var s in declaredSignals) allowedVars.Add(s);
        foreach (var p in declaredPositions) allowedVars.Add(p);
        foreach (var t in declaredTools) allowedVars.Add(t);
        foreach (var b in BuiltInKeywords) allowedVars.Add(b);

        // Collect all defined labels across all snippets & auto-labels
        var definedLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var bTarget in topologyContext?.BackwardTargetStepNumbers ?? new HashSet<int>())
        {
            definedLabels.Add($"lbl_step{bTarget}");
        }

        var gotoNodes = new List<(int StepNum, string Target)>();

        // Tokenize and parse all step snippets
        foreach (var (stepNum, snippetText) in doc.Steps)
        {
            if (string.IsNullOrWhiteSpace(snippetText)) continue;

            var tokens = RapidLexer.Tokenize(snippetText);
            var parser = new RapidParser(tokens);
            var block = parser.ParseBlock();

            // 1. Block balance errors from parser
            foreach (var pErr in parser.ParseErrors)
            {
                errors.Add(new LintError(stepNum, snippetText, pErr));
            }

            // 2. Walk AST for labels, gotos, and identifier scope
            WalkAst(block, stepNum, snippetText, definedLabels, gotoNodes, allowedVars, errors);
        }

        // Also check labels block in SnippetMapDocument
        if (!string.IsNullOrWhiteSpace(doc.Labels))
        {
            var tokens = RapidLexer.Tokenize(doc.Labels);
            var parser = new RapidParser(tokens);
            var block = parser.ParseBlock();
            WalkAst(block, 0, doc.Labels, definedLabels, gotoNodes, allowedVars, errors);
        }

        // 3. Resolve GOTO targets
        foreach (var (stepNum, gotoTarget) in gotoNodes)
        {
            if (!definedLabels.Contains(gotoTarget))
            {
                errors.Add(new LintError(stepNum, $"GOTO {gotoTarget};", $"Target label '{gotoTarget}' does not exist in any step or labels section."));
            }
        }

        return errors;
    }

    private void WalkAst(
        AstNode node,
        int stepNum,
        string snippetText,
        HashSet<string> definedLabels,
        List<(int StepNum, string Target)> gotoNodes,
        HashSet<string> allowedVars,
        List<LintError> errors)
    {
        if (node is BlockNode block)
        {
            foreach (var stmt in block.Statements)
            {
                WalkAst(stmt, stepNum, snippetText, definedLabels, gotoNodes, allowedVars, errors);
            }
        }
        else if (node is LabelNode lblNode)
        {
            definedLabels.Add(lblNode.LabelName);
        }
        else if (node is GotoNode gotoNode)
        {
            gotoNodes.Add((stepNum, gotoNode.TargetLabel));
        }
        else if (node is IfNode ifNode)
        {
            WalkAst(ifNode.ThenBlock, stepNum, snippetText, definedLabels, gotoNodes, allowedVars, errors);
            foreach (var branch in ifNode.ElsifBranches)
            {
                WalkAst(branch.Block, stepNum, snippetText, definedLabels, gotoNodes, allowedVars, errors);
            }
            if (ifNode.ElseBlock != null)
            {
                WalkAst(ifNode.ElseBlock, stepNum, snippetText, definedLabels, gotoNodes, allowedVars, errors);
            }
        }
        else if (node is WhileNode whileNode)
        {
            WalkAst(whileNode.Body, stepNum, snippetText, definedLabels, gotoNodes, allowedVars, errors);
        }
        else if (node is ForNode forNode)
        {
            WalkAst(forNode.Body, stepNum, snippetText, definedLabels, gotoNodes, allowedVars, errors);
        }
        else if (node is TestNode testNode)
        {
            foreach (var c in testNode.Cases)
            {
                WalkAst(c.Block, stepNum, snippetText, definedLabels, gotoNodes, allowedVars, errors);
            }
            if (testNode.DefaultBlock != null)
            {
                WalkAst(testNode.DefaultBlock, stepNum, snippetText, definedLabels, gotoNodes, allowedVars, errors);
            }
        }
    }
}
