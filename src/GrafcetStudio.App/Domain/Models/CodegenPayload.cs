using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Linq;

namespace GrafcetStudio.Domain.Models;

public class CodegenPayload
{
    [JsonPropertyName("Platform")]
    public string Platform { get; set; } = string.Empty;

    [JsonPropertyName("templateRootPath")]
    public string TemplateRootPath { get; set; } = string.Empty;

    [JsonPropertyName("Project")]
    public ProjectInfo? Project { get; set; }

    [JsonPropertyName("Diagram")]
    public DiagramInfo? Diagram { get; set; }

    [JsonPropertyName("Steps")]
    public List<Step> Steps { get; set; } = new();

    [JsonPropertyName("Transitions")]
    public List<Transition> Transitions { get; set; } = new();

    [JsonPropertyName("Variables")]
    public List<DeviceVariable> Variables { get; set; } = new();

    [JsonPropertyName("DeviceTypes")]
    public List<DeviceType> DeviceTypes { get; set; } = new();

    public void EnrichVariables()
    {
        // Replace internal signal IDs with friendly names for each variable
        foreach (var variable in Variables)
        {
            // Find matching device type based on variable.Format (e.g., "Cylinder")
            var deviceType = DeviceTypes?.FirstOrDefault(dt =>
                string.Equals(dt.Name, variable.Format, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(dt.Name, variable.Format, StringComparison.OrdinalIgnoreCase));
            if (deviceType == null) continue;

            var newMap = new Dictionary<string, string>();
            foreach (var kv in variable.SignalAddresses)
            {
                // kv.Key is internal ID like "cyl_lsh"
                // Find signal with that ID in the device type
                var signal = deviceType.Signals.FirstOrDefault(s =>
                    string.Equals(s.Id, kv.Key, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(s.Name, kv.Key, StringComparison.OrdinalIgnoreCase));
                if (signal != null)
                {
                    // Use friendly name as key, value remains the address
                    newMap[signal.Name] = kv.Value;
                }
                else
                {
                    // Keep original entry if not found (fallback)
                    newMap[kv.Key] = kv.Value;
                }
            }
            // Replace with the new map containing only friendly names (replace IDs)
            variable.SignalAddresses = newMap;
        }
    }
}

public class ProjectInfo
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("machineName")] public string? MachineName { get; set; }
}

public class DiagramInfo
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("mode")] public string? Mode { get; set; }
    [JsonPropertyName("unitId")] public string? UnitId { get; set; }
    [JsonPropertyName("unit")] public string? Unit { get; set; }
}
