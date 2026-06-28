# Siemens TIA Openness Import Setup

GrafcetStudio can generate Siemens Openness XML from the `siemens-lad` generator. Direct push/import into TIA Portal is implemented as an optional runtime adapter so normal CI/dev builds do not require Siemens TIA Portal assemblies.

## Load strategy

The main app uses `BridgeSiemensTiaProjectService` and delegates direct import to the external `.NET Framework 4.8` bridge. `Siemens.Engineering.dll` is only loaded by the bridge process or by the legacy reflection mode when explicitly enabled.

- Default mode: `GRAFCETSTUDIO_TIA_IMPORT_MODE=bridge` (or unset), which uses `BridgeSiemensTiaProjectService` and keeps Siemens assemblies out of the main `.NET 8` app.
- Legacy mode: set `GRAFCETSTUDIO_TIA_IMPORT_MODE=reflection` only for troubleshooting or experiments on a TIA-enabled machine.
- Assembly path: set `GRAFCETSTUDIO_TIA_OPENNESS_DIR` to the TIA Portal PublicAPI folder that contains `Siemens.Engineering.dll`.

Typical Siemens PublicAPI locations vary by installed version, for example:

```powershell
$env:GRAFCETSTUDIO_TIA_IMPORT_MODE = "reflection"
$env:GRAFCETSTUDIO_TIA_OPENNESS_DIR = "C:\Program Files\Siemens\Automation\Portal V17\PublicAPI\V17"
```

Adjust `Portal V17` / `V17` for the installed TIA version.

## Required permissions

TIA Openness requires Windows user permissions before external applications can automate TIA Portal.

1. Install TIA Portal with the Openness feature enabled.
2. Add the current Windows user to the Siemens TIA Openness local user group for the installed version.
3. Sign out/sign in or restart Windows so the group membership is refreshed.
4. Start TIA Portal once and confirm Openness prompts/security settings if shown.
5. Run GrafcetStudio under the same Windows user.

If permissions are missing, GrafcetStudio should return `TiaOpennessUnavailable` with a message explaining that Openness access was denied.

## Import request mapping

`SiemensPushRequest` maps to the TIA import operation:

- `ProjectPath`: optional path to open when no matching project is already open.
- `DeviceName`: TIA device/station name to search in the project.
- `PlcName`: PLC/device item name whose `PlcSoftware` owns the block group.
- `TargetFolderPath`: block folder path under Program blocks, for example `Program blocks/Grafcet`.
- `BlockName`: expected block name, used for logging/results.
- `XmlContent`: generated XML content; written to a temporary XML file before import.
- `XmlPath`: existing XML file path; used directly when provided.
- `OverwriteMode`: maps best-effort to TIA import options (`Override`/`Overwrite`, `Rename`, or default/no override).

## Current implementation notes

The bridge-based adapter performs this flow:

1. Generate/request a temporary JSON request file and invoke `GrafcetStudio.TiaBridge.V19.exe`.
2. Let the `.NET Framework 4.8` bridge load `Siemens.Engineering.dll` and attach/open TIA Portal.
3. Pass `ProjectPath`, `DeviceName`, `PlcName`, `TargetFolderPath`, `BlockName`, and `OverwriteMode` to the bridge over the shared contract.
4. Parse bridge stdout JSON, stderr, and exit code into `SiemensPushResult`.
5. Clean up temporary request/XML files and preserve manual XML fallback when the bridge is unavailable.
6. Return a typed `SiemensPushResult` instead of crashing the app.

The app-side adapter intentionally avoids compile-time references to Siemens assemblies. If Siemens changes method signatures between versions, keep the main abstraction and replace this implementation with either:

- a version-specific reflection adapter,
- a separate bridge executable built on a TIA machine, or
- a conditionally compiled project that references Siemens assemblies only on TIA-enabled build agents.

## Integration test notes

This repository environment does not include TIA Portal, so direct import cannot be verified here. Manual validation on a TIA machine should cover:

- TIA not installed: returns `TiaOpennessUnavailable`.
- User missing Openness group membership: returns a permission-focused `TiaOpennessUnavailable` message.
- Wrong `ProjectPath`: returns `ProjectNotFound`.
- Wrong `DeviceName`: returns `DeviceNotFound`.
- Wrong `PlcName`: returns `PlcNotFound`.
- Wrong `TargetFolderPath`: returns `TargetFolderNotFound`.
- Valid Siemens LAD XML import with `FailIfExists`, `Overwrite`, and `Rename` modes.




