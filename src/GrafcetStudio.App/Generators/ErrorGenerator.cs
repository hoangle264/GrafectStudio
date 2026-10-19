using GrafcetStudio.Domain.Models;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GrafcetStudio.App.Generators;

public interface IErrorGenerator
{
    string Generate(CodegenPayload payload);
}

public sealed class ErrorGenerator : IErrorGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>Các kiểu dữ liệu nguyên thủy (primitive) cần lọc bỏ khỏi deviceGroups.</summary>
    private static readonly HashSet<string> PrimitiveFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "BOOL", "BOOLEAN",
        "BYTE", "USINT",
        "WORD", "INT", "UINT",
        "DWORD", "DINT", "UDINT",
        "LWORD",
        "REAL", "LREAL",
        "TIME", "TIMER",
        "STRING", "WSTRING",
        "COUNTER",
        "SystemControl"  // được tách riêng thành field systemControl
    };

    public string Generate(CodegenPayload payload)
    {
        var errors = payload.Flows
            .SelectMany(flow => flow.Transitions)
            .Select(transition => new
            {
                id = transition.Id,
                label = transition.Label,
                condition = transition.Condition,
                fromStepIds = transition.FromStepIds,
                toStepIds = transition.ToStepIds
            })
            .ToList();

        var deviceGroups = (payload.Variables ?? Enumerable.Empty<DeviceVariable>())
            .Where(v => !PrimitiveFormats.Contains(v.Format))
            .GroupBy(v => v.Format)
            .Select(g => new
            {
                type = g.Key,
                devices = g.Select(v => new
                {
                    name = v.Label,
                    label = v.Label,
                    address = v.Address,
                    signalAddresses = v.SignalAddresses
                }).ToList()
            })
            .ToList();

        // Dùng JsonNode để có thể bỏ qua field "unit" khi null
        var result = new JsonObject
        {
            ["errors"]       = JsonSerializer.SerializeToNode(errors, JsonOptions),
            ["deviceGroups"] = JsonSerializer.SerializeToNode(deviceGroups, JsonOptions)
        };

        // Helper giải quyết signalAddresses của 1 UnitInfo
        IDictionary<string, string> ResolveUnitSignalAddresses(UnitInfo u)
        {
            var unitId = u.Id ?? string.Empty;
            if (payload.UnitConfig != null && payload.UnitConfig.TryGetValue(unitId, out var cfg) && cfg?.SignalAddresses is { Count: > 0 } cfgAddresses)
            {
                return cfgAddresses;
            }

            var unitVar = (payload.Variables ?? Enumerable.Empty<DeviceVariable>())
                .FirstOrDefault(v => string.Equals(v.Format, "Unit Station", StringComparison.OrdinalIgnoreCase)
                                  && (string.Equals(v.Label, u.Label, StringComparison.OrdinalIgnoreCase)
                                   || string.Equals(v.Label, u.Name, StringComparison.OrdinalIgnoreCase)
                                   || string.Equals(v.Label, u.Id, StringComparison.OrdinalIgnoreCase)));

            unitVar ??= (payload.Variables ?? Enumerable.Empty<DeviceVariable>())
                .FirstOrDefault(v => string.Equals(v.Format, "Unit Station", StringComparison.OrdinalIgnoreCase));

            return unitVar?.SignalAddresses ?? new Dictionary<string, string>();
        }

        // 1. Tạo field "unit" đơn lẻ nếu payload.Unit != null (tương thích ngược)
        if (payload.Unit is not null)
        {
            var signalAddresses = ResolveUnitSignalAddresses(payload.Unit);

            // Devices được sử dụng trong unit: lọc bỏ primitive và Unit Station
            var unitDevices = (payload.Variables ?? Enumerable.Empty<DeviceVariable>())
                .Where(v => !PrimitiveFormats.Contains(v.Format)
                         && !string.Equals(v.Format, "Unit Station", StringComparison.OrdinalIgnoreCase))
                .Select(v => new
                {
                    name            = v.Label,
                    label           = v.Label,
                    format          = v.Format,
                    address         = v.Address,
                    signalAddresses = v.SignalAddresses
                })
                .ToList();

            result["unit"] = JsonSerializer.SerializeToNode(new
            {
                id              = payload.Unit.Id,
                label           = payload.Unit.Label ?? payload.Unit.Name,
                signalAddresses = signalAddresses,
                devices         = unitDevices
            }, JsonOptions);
        }

        // 2. Tạo field "units" (danh sách tất cả các Unit trong project)
        var allUnits = (payload.Units ?? Enumerable.Empty<UnitInfo>()).ToList();
        if (allUnits.Count == 0 && payload.Unit is not null)
        {
            allUnits.Add(payload.Unit);
        }

        if (allUnits.Count > 0)
        {
            var unitsList = allUnits.Select(u => new
            {
                id              = u.Id,
                name            = u.Name,
                label           = u.Label ?? u.Name,
                signalAddresses = ResolveUnitSignalAddresses(u),
                devices         = (payload.Variables ?? Enumerable.Empty<DeviceVariable>())
                    .Where(v => !PrimitiveFormats.Contains(v.Format)
                             && !string.Equals(v.Format, "Unit Station", StringComparison.OrdinalIgnoreCase))
                    .Select(v => new
                    {
                        name            = v.Label,
                        label           = v.Label,
                        format          = v.Format,
                        address         = v.Address,
                        signalAddresses = v.SignalAddresses
                    })
                    .ToList()
            }).ToList();

            result["units"] = JsonSerializer.SerializeToNode(unitsList, JsonOptions);
        }

        var sysCtrl = (payload.Variables ?? Enumerable.Empty<DeviceVariable>())
            .FirstOrDefault(v => string.Equals(v.Format, "SystemControl", StringComparison.OrdinalIgnoreCase));

        if (sysCtrl is not null)
        {
            result["systemControl"] = JsonSerializer.SerializeToNode(new
            {
                label           = sysCtrl.Label,
                signalAddresses = sysCtrl.SignalAddresses
            }, JsonOptions);
        }

        return result.ToJsonString(JsonOptions);
    }
}
