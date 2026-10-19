namespace GrafcetStudio.TiaBridge.Contracts;

public enum TiaBridgeStatus
{
    Success,
    InvalidRequest,
    BridgeNotFound,
    TiaNotInstalled,
    TiaAccessDenied,
    ProjectNotFound,
    DeviceNotFound,
    PlcNotFound,
    TargetFolderNotFound,
    XmlNotFound,
    ImportFailed,
    Timeout,
    UnexpectedError
}
