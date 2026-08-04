using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GrafcetStudio.App.Services.Ai;
using GrafcetStudio.Domain.Models;

namespace GrafcetStudio.App.Generators.Robot;

public class AbbRapidGenerator : ICodeGenerator
{
    public string Platform => "abb-rapid";

    private readonly RobotPromptBuilder _promptBuilder;
    private readonly RobotIrValidator _validator;
    private readonly RapidRenderer _renderer;
    private readonly TeachListGenerator _teachListGenerator;
    private readonly IAiCompletionService? _aiCompletionService;

    public AbbRapidGenerator(IAiCompletionService? aiCompletionService = null)
    {
        _promptBuilder = new RobotPromptBuilder();
        _validator = new RobotIrValidator();
        _renderer = new RapidRenderer();
        _teachListGenerator = new TeachListGenerator();
        _aiCompletionService = aiCompletionService;
    }

    public IEnumerable<CodegenFile> GenerateFiles(CodegenPayload payload)
    {
        var targetFlow = payload.Flows?.FirstOrDefault()?.Name ?? "main";
        var flowNameClean = SanitizeFilename(targetFlow);

        var (systemPrompt, userPrompt) = _promptBuilder.BuildPrompts(payload, flowNameClean);

        string rawJsonResponse;

        if (_aiCompletionService != null && _aiCompletionService is not MockAiCompletionService)
        {
            try
            {
                var jsonPayload = System.Text.Json.JsonSerializer.Serialize(new { systemPrompt, userPrompt });
                var sanitized = new SanitizedAiRequest("robot-gen-1", "robot-codegen", userPrompt, jsonPayload);
                var req = new AiCompletionRequest(sanitized);
                var result = Task.Run(() => _aiCompletionService.CompleteAsync(req)).GetAwaiter().GetResult();
                
                if (result.Ok && !string.IsNullOrWhiteSpace(result.RawText) && _validator.Validate(result.RawText).IsValid)
                {
                    rawJsonResponse = result.RawText;
                }
                else
                {
                    rawJsonResponse = GenerateFallbackJson(payload, flowNameClean);
                }
            }
            catch
            {
                rawJsonResponse = GenerateFallbackJson(payload, flowNameClean);
            }
        }
        else
        {
            rawJsonResponse = GenerateFallbackJson(payload, flowNameClean);
        }

        var validationResult = _validator.Validate(rawJsonResponse);

        if (!validationResult.IsValid)
        {
            var errContent = $"! ============================================\n" +
                             $"! ROBOT CODEGEN VALIDATION ERROR(S)\n" +
                             $"! ============================================\n" +
                             string.Join("\n", validationResult.Errors.Select(e => $"! Error: {e}"));

            return new List<CodegenFile>
            {
                new CodegenFile { Path = $"{flowNameClean}.mod", Content = errContent }
            };
        }

        var doc = validationResult.Document!;
        var rapidCode = _renderer.Render(doc, flowNameClean);
        var teachList = _teachListGenerator.GenerateTeachList(doc, flowNameClean);

        return new List<CodegenFile>
        {
            new CodegenFile
            {
                Path = $"{flowNameClean}.mod",
                Content = rapidCode
            },
            new CodegenFile
            {
                Path = $"{flowNameClean}_teach.txt",
                Content = teachList
            }
        };
    }

    private string GenerateFallbackJson(CodegenPayload payload, string flowName, string? errorContext = null)
    {
        var posVars = payload.Variables?.Where(v => string.Equals(v.Format, "POS", StringComparison.OrdinalIgnoreCase) || string.Equals(v.Format, "Position", StringComparison.OrdinalIgnoreCase)).Select(v => v.Label).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        var positions = (posVars != null && posVars.Count > 0) ? posVars : new List<string> { "pHome", "pWait", "pPickup", "pPlace" };
        if (!positions.Contains("pHome", StringComparer.OrdinalIgnoreCase)) positions.Insert(0, "pHome");
        if (!positions.Contains("pWait", StringComparer.OrdinalIgnoreCase)) positions.Add("pWait");
        if (!positions.Contains("pPickup", StringComparer.OrdinalIgnoreCase)) positions.Add("pPickup");
        if (!positions.Contains("pPlace", StringComparer.OrdinalIgnoreCase)) positions.Add("pPlace");

        var posObjects = positions.Select(p => new RobotPosition
        {
            Name = p,
            MotionType = p.Equals("pHome", StringComparison.OrdinalIgnoreCase) ? "AbsJ" : (p.Equals("pWait", StringComparison.OrdinalIgnoreCase) ? "Joint" : "Linear"),
            Speed = (p.Equals("pPickup", StringComparison.OrdinalIgnoreCase) || p.Equals("pPlace", StringComparison.OrdinalIgnoreCase)) ? "Precise" : "Medium",
            Description = $"Robot position target {p}"
        }).ToList();

        var signals = new List<RobotSignal>
        {
            new RobotSignal { Name = "gripper_open", Type = "DO", Alias = "DO1" },
            new RobotSignal { Name = "sensor_part_present", Type = "DI", Alias = "DI1" }
        };

        var doc = new RobotIrDocument
        {
            Schema = "grafectstudio/robot-ir/v1",
            Platform = "ABB_RAPID",
            Module = flowName,
            Signals = signals,
            Positions = posObjects,
            Tools = new List<RobotTool> { new RobotTool { Name = "tool1", Description = "Default Gripper" } },
            Init = new RobotInitBlock
            {
                Instructions = new List<RobotInstruction>
                {
                    new RobotInstruction { Type = "SetDO", Signal = "gripper_open", Value = 0 },
                    new RobotInstruction { Type = "WaitTime", Seconds = 0.3 },
                    new RobotInstruction { Type = "MoveAbsJ", Target = "pHome", Tool = "tool1" }
                }
            },
            Steps = new List<RobotStep>()
        };

        int stepIdx = 1;
        if (payload.Flows != null && payload.Flows.Count > 0)
        {
            foreach (var flow in payload.Flows)
            {
                foreach (var step in flow.Steps)
                {
                    var instrList = new List<RobotInstruction>();
                    if (stepIdx == 1)
                    {
                        instrList.Add(new RobotInstruction { Type = "WaitDI", Signal = "sensor_part_present", Value = 1 });
                    }
                    else if (stepIdx == 2)
                    {
                        instrList.Add(new RobotInstruction { Type = "MoveJ", Target = "pWait", Tool = "tool1" });
                        instrList.Add(new RobotInstruction { Type = "MoveL", Target = "pPickup", Tool = "tool1" });
                    }
                    else if (stepIdx == 3)
                    {
                        instrList.Add(new RobotInstruction { Type = "SetDO", Signal = "gripper_open", Value = 1 });
                        instrList.Add(new RobotInstruction { Type = "WaitTime", Seconds = 0.5 });
                    }
                    else if (stepIdx == 4)
                    {
                        instrList.Add(new RobotInstruction { Type = "MoveL", Target = "pWait", Tool = "tool1" });
                        instrList.Add(new RobotInstruction { Type = "MoveJ", Target = "pPlace", Tool = "tool1" });
                        instrList.Add(new RobotInstruction { Type = "SetDO", Signal = "gripper_open", Value = 0 });
                        instrList.Add(new RobotInstruction { Type = "WaitTime", Seconds = 0.3 });
                    }
                    else
                    {
                        instrList.Add(new RobotInstruction { Type = "MoveJ", Target = "pHome", Tool = "tool1" });
                    }

                    doc.Steps.Add(new RobotStep
                    {
                        Id = stepIdx,
                        Name = string.IsNullOrWhiteSpace(step.Label) ? $"Step_{step.Number}" : step.Label,
                        GrafectStepId = step.Number,
                        Instructions = instrList
                    });
                    stepIdx++;
                }
            }
        }

        if (doc.Steps.Count == 0)
        {
            doc.Steps.Add(new RobotStep
            {
                Id = 1,
                Name = "Default Move",
                GrafectStepId = 1,
                Instructions = new List<RobotInstruction>
                {
                    new RobotInstruction { Type = "MoveJ", Target = "pHome", Tool = "tool1" }
                }
            });
        }

        return System.Text.Json.JsonSerializer.Serialize(doc, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }

    private static string SanitizeFilename(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "main";
        var invalidChars = Path.GetInvalidFileNameChars();
        var clean = new string(name.Where(c => !invalidChars.Contains(c)).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(clean) ? "main" : clean;
    }
}
