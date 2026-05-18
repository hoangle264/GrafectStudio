using System.Collections.Generic;
using System.Linq;

namespace GrafcetStudio.CodeGen.Profile;

/// <summary>Provides built-in PLC profiles and profile lookup operations.</summary>
public static class ProfileRegistry
{
    public static PlcProfile Kv5500 { get; } = Create("kv-5500", "KEYENCE KV-5500", ".mnm", ";");

    public static PlcProfile Kv8000 { get; } = Create("kv-8000", "KEYENCE KV-8000", ".mnm", ";");

    public static PlcProfile Melsec { get; } = Create("melsec", "Mitsubishi MELSEC", ".awl", ";", new Dictionary<string, string>
    {
        ["LDNOT"] = "LDI",
        ["RST"] = "RST",
        ["ANB"] = "AND LD"
    });

    public static PlcProfile Omron { get; } = Create("omron", "OMRON", ".txt", ";", new Dictionary<string, string>
    {
        ["LDNOT"] = "LD NOT",
        ["RST"] = "RSET"
    });

    public static PlcProfile Siemens { get; } = Create("siemens", "Siemens", ".awl", "//", new Dictionary<string, string>
    {
        ["LD"] = "A",
        ["LDNOT"] = "AN",
        ["AND"] = "A",
        ["OR"] = "O",
        ["OUT"] = "=",
        ["RST"] = "R",
        ["SET"] = "S"
    });

    public static IReadOnlyDictionary<string, PlcProfile> All { get; } = new Dictionary<string, PlcProfile>
    {
        [Kv5500.Id] = Kv5500,
        [Kv8000.Id] = Kv8000,
        [Melsec.Id] = Melsec,
        [Omron.Id] = Omron,
        [Siemens.Id] = Siemens
    };

    public static PlcProfile Get(string id)
        => All.TryGetValue(id, out var profile) ? profile : Kv8000;

    public static string ApplyProfile(string code, PlcProfile profile)
    {
        foreach (var pair in profile.InstructionMap.OrderByDescending(p => p.Key.Length))
        {
            code = code.Replace(pair.Key, pair.Value);
        }

        return code;
    }

    private static PlcProfile Create(string id, string label, string ext, string commentPrefix, IDictionary<string, string>? map = null)
        => new()
        {
            Id = id,
            Label = label,
            FileExtension = ext,
            CommentPrefix = commentPrefix,
            InstructionMap = map ?? new Dictionary<string, string>(),
            TimerInstruction = (ms, timerAddr) => $"TMR {timerAddr} {ms:0}"
        };
}
