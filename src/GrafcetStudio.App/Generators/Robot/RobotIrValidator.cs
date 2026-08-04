using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace GrafcetStudio.App.Generators.Robot;

public class ValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public List<string> Errors { get; } = new();
    public RobotIrDocument? Document { get; set; }
}

public class RobotIrValidator
{
    private static readonly HashSet<string> WhitelistInstructions = new(StringComparer.OrdinalIgnoreCase)
    {
        "MoveJ",
        "MoveL",
        "MoveAbsJ",
        "SetDO",
        "WaitDI",
        "WaitTime",
        "SetAO",
        "WaitAI"
    };

    public ValidationResult Validate(string rawJson)
    {
        var result = new ValidationResult();

        if (string.IsNullOrWhiteSpace(rawJson))
        {
            result.Errors.Add("JSON response is empty or whitespace.");
            return result;
        }

        RobotIrDocument? doc;
        try
        {
            // Strip markdown code fence ```json if AI enclosed it
            var cleanJson = rawJson.Trim();
            if (cleanJson.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            {
                cleanJson = cleanJson.Substring(7);
            }
            else if (cleanJson.StartsWith("```"))
            {
                cleanJson = cleanJson.Substring(3);
            }
            if (cleanJson.EndsWith("```"))
            {
                cleanJson = cleanJson.Substring(0, cleanJson.Length - 3);
            }
            cleanJson = cleanJson.Trim();

            doc = JsonSerializer.Deserialize<RobotIrDocument>(cleanJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            result.Errors.Add($"JSON Parsing failed: {ex.Message}");
            return result;
        }

        if (doc == null)
        {
            result.Errors.Add("Deserialized Robot JSON IR document is null.");
            return result;
        }

        result.Document = doc;

        if (!string.Equals(doc.Schema, "grafectstudio/robot-ir/v1", StringComparison.OrdinalIgnoreCase))
        {
            result.Errors.Add($"Invalid Schema '{doc.Schema}'. Expected 'grafectstudio/robot-ir/v1'.");
        }

        if (doc.Steps == null || doc.Steps.Count == 0)
        {
            result.Errors.Add("JSON IR contains no steps.");
        }

        var declaredPositions = new HashSet<string>(doc.Positions.Select(p => p.Name), StringComparer.OrdinalIgnoreCase);
        var declaredSignals = new HashSet<string>(doc.Signals.Select(s => s.Name), StringComparer.OrdinalIgnoreCase);

        var allInstructions = (doc.Init?.Instructions ?? new List<RobotInstruction>())
            .Concat(doc.Steps?.SelectMany(s => s.Instructions ?? new List<RobotInstruction>()) ?? Enumerable.Empty<RobotInstruction>());

        int instrIndex = 0;
        foreach (var instr in allInstructions)
        {
            instrIndex++;
            if (string.IsNullOrWhiteSpace(instr.Type))
            {
                result.Errors.Add($"Instruction #{instrIndex} has empty 'type'.");
                continue;
            }

            if (!WhitelistInstructions.Contains(instr.Type))
            {
                result.Errors.Add($"Instruction #{instrIndex} has invalid type '{instr.Type}'. Must be one of: {string.Join(", ", WhitelistInstructions)}.");
            }

            if (!string.IsNullOrWhiteSpace(instr.Target) && !declaredPositions.Contains(instr.Target))
            {
                result.Errors.Add($"Instruction #{instrIndex} ({instr.Type}) targets undeclared position '{instr.Target}'.");
            }

            if (!string.IsNullOrWhiteSpace(instr.Signal) && !declaredSignals.Contains(instr.Signal))
            {
                result.Errors.Add($"Instruction #{instrIndex} ({instr.Type}) targets undeclared signal '{instr.Signal}'.");
            }
        }

        return result;
    }
}
