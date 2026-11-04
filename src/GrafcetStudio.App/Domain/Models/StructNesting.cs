using System;
using System.Collections.Generic;
using System.Linq;

namespace GrafcetStudio.Domain.Models;

/// <summary>A leaf member of a struct, addressed by its dotted path from the struct root ("Sensor.Fwd").</summary>
public sealed record StructLeaf(string Path, DeviceSignal Signal, int Depth);

/// <summary>
/// Walks structs that hold other structs. A signal is struct-typed when <see cref="DeviceSignal.NestedTypeId"/>
/// (preferred) or its data type names another <see cref="DeviceType"/>. Mirrors web/ts/core/struct-nesting.ts.
/// </summary>
public static class StructNesting
{
    private static readonly HashSet<string> PrimitiveTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Bool", "Int", "DInt", "UInt", "UDInt", "Real", "LReal", "Word", "DWord", "Byte", "String", "Time"
    };

    public static DeviceType? FindSignalType(IEnumerable<DeviceType> deviceTypes, DeviceSignal? signal)
    {
        if (signal is null) return null;
        var types = deviceTypes as IList<DeviceType> ?? deviceTypes.ToList();

        if (!string.IsNullOrWhiteSpace(signal.NestedTypeId))
        {
            var byId = types.FirstOrDefault(type => string.Equals(type.Id, signal.NestedTypeId, StringComparison.OrdinalIgnoreCase));
            if (byId is not null) return byId;
        }

        var typeName = (signal.DataType ?? string.Empty).Trim();
        if (typeName.Length == 0 || PrimitiveTypes.Contains(typeName)) return null;
        return types.FirstOrDefault(type => string.Equals(type.Name, typeName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Leaf members of <paramref name="root"/>. Expansion stops at <see cref="StructLimits.MaxNestingDepth"/>
    /// levels and at loops; a struct-typed member that is not expanded is returned as a leaf.
    /// </summary>
    public static IReadOnlyList<StructLeaf> FlattenLeaves(DeviceType root, IEnumerable<DeviceType> deviceTypes)
    {
        var types = deviceTypes as IList<DeviceType> ?? deviceTypes.ToList();
        var leaves = new List<StructLeaf>();
        Walk(root, types, string.Empty, 1, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { root.Id },
            (path, signal, depth, child) =>
            {
                if (child is null) leaves.Add(new StructLeaf(path, signal, depth));
                return child is not null;
            },
            (path, signal, depth) => leaves.Add(new StructLeaf(path, signal, depth)));
        return leaves;
    }

    /// <summary>Paths of struct-typed members that were expanded ("Sensor", "Sensor.Probe"); they never carry an address.</summary>
    public static IReadOnlyList<string> ContainerPaths(DeviceType root, IEnumerable<DeviceType> deviceTypes)
    {
        var types = deviceTypes as IList<DeviceType> ?? deviceTypes.ToList();
        var paths = new List<string>();
        Walk(root, types, string.Empty, 1, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { root.Id },
            (path, _, _, child) =>
            {
                if (child is not null) paths.Add(path);
                return child is not null;
            },
            (_, _, _) => { });
        return paths;
    }

    /// <param name="visit">Called per member; return true to descend into <c>child</c>.</param>
    /// <param name="notExpanded">Called for a struct-typed member that hits the depth limit or a loop.</param>
    private static void Walk(
        DeviceType current,
        IList<DeviceType> types,
        string prefix,
        int depth,
        HashSet<string> stack,
        Func<string, DeviceSignal, int, DeviceType?, bool> visit,
        Action<string, DeviceSignal, int> notExpanded)
    {
        foreach (var signal in current.Signals ?? new List<DeviceSignal>())
        {
            var name = string.IsNullOrWhiteSpace(signal.Name) ? signal.Id : signal.Name;
            if (string.IsNullOrWhiteSpace(name)) continue;

            var path = prefix.Length == 0 ? name : prefix + "." + name;
            var child = FindSignalType(types, signal);
            if (child is not null && (depth >= StructLimits.MaxNestingDepth || stack.Contains(child.Id)))
            {
                notExpanded(path, signal, depth);
                continue;
            }

            if (!visit(path, signal, depth, child) || child is null) continue;

            stack.Add(child.Id);
            Walk(child, types, path, depth + 1, stack, visit, notExpanded);
            stack.Remove(child.Id);
        }
    }
}
