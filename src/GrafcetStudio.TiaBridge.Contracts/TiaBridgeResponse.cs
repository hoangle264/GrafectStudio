namespace GrafcetStudio.TiaBridge.Contracts;

public sealed class TiaBridgeResponse
{
    public bool Success { get; set; }

    public TiaBridgeStatus Status { get; set; } = TiaBridgeStatus.UnexpectedError;

    public string Message { get; set; } = string.Empty;

    public string? Details { get; set; }

    public string? ImportedPath { get; set; }

    public string? TiaVersion { get; set; }

    public int? ExitCode { get; set; }
}
