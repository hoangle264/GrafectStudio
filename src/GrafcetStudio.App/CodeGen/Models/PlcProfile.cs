namespace GrafcetStudio.CodeGen.Profile;

/// <summary>Represents PLC-specific mnemonics and instruction mappings.</summary>
public class PlcProfile
{
    public string Id { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public string FileExtension { get; init; } = ".txt";

    public string CommentPrefix { get; init; } = ";";

    public IDictionary<string, string> InstructionMap { get; init; } = new Dictionary<string, string>();

    public Func<double, string, string> TimerInstruction { get; init; } = (ms, timerAddr) => $"TMR {timerAddr} {ms:0}";
}
