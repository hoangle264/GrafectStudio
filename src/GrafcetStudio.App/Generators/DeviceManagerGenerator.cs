using GrafcetStudio.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace GrafcetStudio.App.Generators;

public interface IDeviceManagerGenerator
{
    string Generate(CodegenPayload payload);
}

public sealed class DeviceManagerGenerator : IDeviceManagerGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string Generate(CodegenPayload payload)
    {
        var deviceTypes = payload.DeviceTypes
            .GroupBy(deviceType => deviceType.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                type = group.Key,
                file = $"DeviceManager_{NormalizeDeviceKind(group.Key)}.st",
                signals = group.SelectMany(deviceType => deviceType.Signals).Select(signal => new
                {
                    id = signal.Id,
                    name = signal.Name,
                    dataType = signal.DataType,
                    varType = signal.VarType.ToString(),
                    comment = signal.Comment
                }).ToList()
            })
            .OrderBy(item => item.type, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return JsonSerializer.Serialize(new { deviceTypes }, JsonOptions);
    }

    private static string NormalizeDeviceKind(string? format)
    {
        if (string.IsNullOrWhiteSpace(format)) return "generic";
        return new string(format.Trim().ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray()).Trim('_');
    }
}
