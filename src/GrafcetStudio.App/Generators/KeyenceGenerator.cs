using GrafcetStudio.App.Generators.Common;
using GrafcetStudio.App.Generators.Keyence;
using GrafcetStudio.CodeGen.Runtime.Models;
using GrafcetStudio.CodeGen.Template;
using GrafcetStudio.Domain.Models;
using GrafcetStudio.Domain.Resolution;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace GrafcetStudio.App.Generators;

public class KeyenceGenerator : LegacyCodeGeneratorBase
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly string[] SectionTemplateOrder =
    [
        "uc.error",
        "uc.manual",
        "uc.origin",
        "uc.auto"
    ];

    private static readonly string[] ExpressionSectionTemplateOrder =
    [
        "uc.main",
        "uc.flows"
    ];

    private static readonly (string TemplateId, string PartialName)[] KnownPartials =
    [
        ("uc.flow", "flow"),
        ("uc.step", "step"),
        ("uc.stepExpression", "step_expression"),
        ("uc.actionExpression", "action_expression"),
        ("uc.outputExpression", "output_expression"),
        ("uc.transitionExpression", "transition_expression"),
        ("uc.stepBody", "step_body"),
        ("uc.deviceCylinder", "device_cylinder"),
        ("uc.deviceServo", "device_servo"),
        ("uc.deviceMotor", "device_motor"),
        ("uc.deviceGeneric", "device_generic")
    ];
    private readonly TemplateManager _templates;
    private readonly ISequenceResolver _sequenceResolver;

    public KeyenceGenerator(TemplateManager templates, ISequenceResolver sequenceResolver)
    {
        _templates = templates;
        _sequenceResolver = sequenceResolver;
    }

    public override string Platform => "Keyence";
    public string GenerateUnitContent(CodegenPayload payload) => GenerateLegacy(payload);
    protected override string GenerateLegacy(CodegenPayload payload)
    {
         var context = BuildContext(payload);
        RegisterPartials();

        var renderedSections = ResolveSectionTemplateNames()
            .Select(templateName => _templates.TryRender(templateName, context, out var result) ? result : string.Empty)
            .Where(section => !string.IsNullOrWhiteSpace(section))
            .ToList();

        if (renderedSections.Count == 0)
        {
            return JsonSerializer.Serialize(context, JsonOptions);
        }

        var rendered = string.Join(Environment.NewLine, renderedSections);
        return RenderedOutputLooksExpressionBased(rendered)
            ? Keyence.MnemonicEmitter.ConvertPseudoExpressions(rendered, payload.Variables)
            : rendered;
    }


    private static bool RenderedOutputLooksExpressionBased(string rendered)
        => !string.IsNullOrWhiteSpace(rendered)
            && (rendered.Contains("->", StringComparison.Ordinal)
                || rendered.Contains("-&gt;", StringComparison.OrdinalIgnoreCase));
    private IEnumerable<string> ResolveSectionTemplateNames()
    {
        if (_templates.IsTemplateLoaded("uc.main"))
        {
            yield return "uc.main";
            yield break;
        }

        if (_templates.IsTemplateLoaded("uc.flows"))
        {
            yield return "uc.flows";
        }
        else
        {
            foreach (var templateName in SectionTemplateOrder)
            {
                if (_templates.IsTemplateLoaded(templateName)) yield return templateName;
            }
        }

        if (_templates.IsTemplateLoaded("uc.outputs"))
        {
            yield return "uc.outputs";
        }
        else if (_templates.IsTemplateLoaded("uc.mainOutput"))
        {
            yield return "uc.mainOutput";
        }
        else if (_templates.IsTemplateLoaded("uc.outputLegacy"))
        {
            yield return "uc.outputLegacy";
        }
    }

    private void RegisterPartials()
    {
        foreach (var templateName in ExpressionSectionTemplateOrder)
        {
            RegisterPartialIfLoaded(templateName, templateName[3..]);
        }

        foreach (var (templateId, partialName) in KnownPartials)
        {
            RegisterPartialIfLoaded(templateId, partialName);
        }

        foreach (var templateId in _templates.GetLoadedTemplateIds().Where(id => id.StartsWith("device_", StringComparison.OrdinalIgnoreCase)))
        {
            RegisterPartialIfLoaded(templateId, templateId);
        }
    }

    private void RegisterPartialIfLoaded(string templateId, string partialName)
    {
        if (!_templates.IsTemplateLoaded(templateId) || _templates.IsPartialRegistered(partialName)) return;

        var source = _templates.GetTemplateSource(templateId);
        if (!string.IsNullOrEmpty(source)) _templates.RegisterPartial(partialName, source);
    }

    private GeneratorContext BuildContext(CodegenPayload payload)
        => GeneratorContextBuilder.Build(payload, _sequenceResolver);
}



internal static class StepLabelExtensions
{
    public static string LabelOrId(this Step step) => !string.IsNullOrWhiteSpace(step.Label) ? step.Label : step.Id;
}



