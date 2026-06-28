namespace GrafcetStudio.TiaBridge.Contracts;

public sealed class TiaBridgeRequest
{
    public string TiaVersion { get; set; } = "V19";

    public string? ProjectPath { get; set; }

    public string DeviceName { get; set; } = string.Empty;

    public string PlcName { get; set; } = string.Empty;

    public string TargetFolderPath { get; set; } = string.Empty;

    public string BlockName { get; set; } = string.Empty;

    public string XmlPath { get; set; } = string.Empty;

    public TiaBridgeOverwriteMode OverwriteMode { get; set; } = TiaBridgeOverwriteMode.FailIfExists;
}
