using System;
using System.Collections.Generic;
using System.Linq;

namespace GrafcetStudio.App.Generators.Keyence;

internal static class LinqExtensions
{
    public static IEnumerable<string> Trimmed(this IEnumerable<string?> source)
        => source.Select(value => value?.Trim() ?? string.Empty);

    public static IEnumerable<string> NotEmpty(this IEnumerable<string?> source)
        => source.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!.Trim());

    public static IEnumerable<string> DistinctIgnoreCase(this IEnumerable<string> source)
        => source.Distinct(StringComparer.OrdinalIgnoreCase);

    public static Dictionary<string, TValue> ToDictionaryIgnoreCase<TValue>(
        this IEnumerable<TValue> source,
        Func<TValue, string> keySelector)
        => source.ToDictionary(keySelector, StringComparer.OrdinalIgnoreCase);
}
