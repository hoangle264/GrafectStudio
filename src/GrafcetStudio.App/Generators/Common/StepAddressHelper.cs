using GrafcetStudio.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GrafcetStudio.App.Generators.Common;

public record struct ParsedBoolBase(string Prefix, int Number, int Width);

public record struct StepExecAddress(string Prefix, int Number, bool HasBit, int Bit, long SortValue);

/// <summary>
/// Helpers for computing step address ranges and formatting address strings.
/// Platform-agnostic — does not depend on any Keyence-specific types.
/// </summary>
public static class StepAddressHelper
{
    public static (string MinAddress, string MaxAddress, string SequenceEnd) BuildFlowStepAddressRange(FlowInfo flow)
    {
        var addressedSteps = new List<(Step Step, StepExecAddress Address)>();
        foreach (var step in flow.Steps)
        {
            if (TryParseStepExecAddress(step.ExecAddress, flow.Diagram, out var parsed))
            {
                addressedSteps.Add((step, parsed));
            }
        }

        if (addressedSteps.Count == 0) return (string.Empty, string.Empty, string.Empty);

        var min = addressedSteps.OrderBy(item => item.Address.SortValue).First();
        var max = addressedSteps.OrderByDescending(item => item.Address.SortValue).First();
        var sequenceEnd = ResolveSequenceEnd(max.Step, max.Address, flow.Diagram);

        return (min.Step.ExecAddress ?? string.Empty, max.Step.ExecAddress ?? string.Empty, sequenceEnd);
    }

    public static (string MinAddress, string MaxAddress) BuildUnitStepAddressRange(IList<ResolvedFlow> flows)
    {
        var addresses = new List<(Step Step, StepExecAddress Address)>();
        foreach (var flow in flows)
        {
            foreach (var step in flow.rawSteps)
            {
                if (TryParseStepExecAddress(step.ExecAddress, flow.diagram, out var parsed))
                {
                    addresses.Add((step, parsed));
                }
            }
        }

        if (addresses.Count == 0) return (string.Empty, string.Empty);

        var min = addresses.OrderBy(item => item.Address.SortValue).First();
        var max = addresses.OrderByDescending(item => item.Address.SortValue).First();
        return (min.Step.ExecAddress ?? string.Empty, max.Step.ExecAddress ?? string.Empty);
    }

    public static string ResolveSequenceEnd(Step step, StepExecAddress parsedAddress, DiagramInfo? diagram)
    {
        var stepNumber = step.Number;
        if (stepNumber < 1) return IncrementParsedAddress(parsedAddress);

        if (string.Equals(diagram?.AddressMode, "word", StringComparison.OrdinalIgnoreCase))
        {
            return FormatWordStepExecAddress(diagram?.ActiveWord, stepNumber + 1);
        }

        var parsedBase = TryParseAddressBase(diagram?.BaseMr, out var configuredBase)
            ? configuredBase
            : ResolveBoolBase(parsedAddress, stepNumber, diagram?.BoolAddressMode);
        var offset = stepNumber * 2;
        var nextNumber = ResolveBoolMr(parsedBase.Number, offset, diagram?.BoolAddressMode);
        return FormatAddressBase(parsedBase.Prefix, nextNumber, parsedBase.Width);
    }

    public static ParsedBoolBase ResolveBoolBase(StepExecAddress parsedAddress, int stepNumber, string? boolAddressMode)
    {
        var offset = Math.Max(0, (stepNumber - 1) * 2);
        if (string.Equals(boolAddressMode, "block", StringComparison.OrdinalIgnoreCase))
        {
            return new ParsedBoolBase(parsedAddress.Prefix, parsedAddress.Number - (offset / 8) * 100 - (offset % 8), 0);
        }

        return new ParsedBoolBase(parsedAddress.Prefix, parsedAddress.Number - offset, 0);
    }

    public static int ResolveBoolMr(int baseMr, int offset, string? boolAddressMode)
    {
        return string.Equals(boolAddressMode, "block", StringComparison.OrdinalIgnoreCase)
            ? baseMr + (offset / 8) * 100 + offset % 8
            : baseMr + offset;
    }

    public static string FormatWordStepExecAddress(string? activeWord, int stepNumber)
    {
        if (stepNumber < 1) stepNumber = 1;

        var bitIndex = stepNumber - 1;
        var wordOffset = bitIndex / 16;
        var bit = bitIndex % 16;
        var word = FormatWordAddress(activeWord, wordOffset);
        return $"{word}.{bit}";
    }

    public static string FormatWordAddress(string? baseWord, int offset)
    {
        var value = string.IsNullOrWhiteSpace(baseWord) ? "DM0" : baseWord.Trim().TrimStart('@');
        var prefixLength = value.TakeWhile(char.IsLetter).Count();
        var prefix = prefixLength > 0 ? value[..prefixLength].ToUpperInvariant() : "DM";
        var numberText = value[prefixLength..];
        var number = int.TryParse(numberText, out var parsed) ? parsed : 0;
        var width = numberText.Length > 1 ? numberText.Length : 0;
        var nextNumber = number + offset;
        return width > 0 ? $"{prefix}{nextNumber.ToString().PadLeft(width, '0')}" : $"{prefix}{nextNumber}";
    }

    public static string IncrementParsedAddress(StepExecAddress parsedAddress)
        => parsedAddress.HasBit
            ? $"{parsedAddress.Prefix}{parsedAddress.Number}.{parsedAddress.Bit + 1}"
            : $"{parsedAddress.Prefix}{parsedAddress.Number + 1}";

    public static bool TryParseAddressBase(string? value, out ParsedBoolBase parsed)
    {
        parsed = default;
        var text = (value ?? string.Empty).Trim().TrimStart('@');
        if (string.IsNullOrWhiteSpace(text)) return false;
        var prefixLength = text.TakeWhile(char.IsLetter).Count();
        if (prefixLength <= 0 || prefixLength >= text.Length) return false;
        var prefix = text[..prefixLength].ToUpperInvariant();
        var numberText = text[prefixLength..];
        if (!int.TryParse(numberText, out var number)) return false;
        parsed = new ParsedBoolBase(prefix, number, numberText.Length > 1 ? numberText.Length : 0);
        return true;
    }

    public static string FormatAddressBase(string prefix, int number, int width)
        => width > 0 ? $"{prefix}{number.ToString().PadLeft(width, '0')}" : $"{prefix}{number}";

    public static bool TryParseStepExecAddress(string? address, DiagramInfo? diagram, out StepExecAddress parsed)
    {
        parsed = default;
        var value = (address ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(value)) return false;

        var trimmed = value.TrimStart('@');
        var dotIndex = trimmed.IndexOf('.');
        var head = dotIndex >= 0 ? trimmed[..dotIndex] : trimmed;
        var bitText = dotIndex >= 0 ? trimmed[(dotIndex + 1)..] : string.Empty;
        var prefixLength = head.TakeWhile(char.IsLetter).Count();
        if (prefixLength <= 0 || prefixLength >= head.Length) return false;

        var prefix = head[..prefixLength].ToUpperInvariant();
        if (!int.TryParse(head[prefixLength..], out var number)) return false;
        var hasBit = int.TryParse(bitText, out var bit);
        var sortValue = ResolveAddressSortValue(prefix, number, hasBit ? bit : 0, diagram);
        parsed = new StepExecAddress(prefix, number, hasBit, hasBit ? bit : 0, sortValue);
        return true;
    }

    public static long ResolveAddressSortValue(string prefix, int number, int bit, DiagramInfo? diagram)
    {
        if (string.Equals(prefix, "MR", StringComparison.OrdinalIgnoreCase))
        {
            if (TryParseAddressBase(diagram?.BaseMr, out var baseMr) && string.Equals(diagram?.BoolAddressMode, "block", StringComparison.OrdinalIgnoreCase))
            {
                var relative = number - baseMr.Number;
                if (relative >= 0)
                {
                    return ((long)(relative / 100) * 8) + relative % 100;
                }
            }

            return number;
        }

        return ((long)number * 16) + bit;
    }
}
