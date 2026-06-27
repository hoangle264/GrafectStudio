# Siemens TIA Openness Import Setup

GrafcetStudio can generate Siemens Openness XML from the `siemens-lad` generator. Direct push/import into TIA Portal is implemented as an optional runtime adapter so normal CI/dev builds do not require Siemens TIA Portal assemblies.

## Load strategy

The main app uses `ReflectionSiemensTiaProjectService` and loads `Siemens.Engineering.dll` at runtime only when explicitly enabled.

- Default mode: `UnavailableSiemensTiaProjectService`, safe on machines without TIA Portal.
- Opt-in mode: set `GRAFCETSTUDIO_TIA_OPENNESS_MODE=reflection`.
- Assembly path: set `GRAFCETSTUDIO_TIA_OPENNESS_DIR` to the TIA Portal PublicAPI folder that contains `Siemens.Engineering.dll`.

Typical Siemens PublicAPI locations vary by installed version, for example:

```powershell
$env:GRAFCETSTUDIO_TIA_OPENNESS_MODE = "reflection"
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

The reflection adapter performs this flow:

1. Load `Siemens.Engineering.dll` from `GRAFCETSTUDIO_TIA_OPENNESS_DIR` or normal assembly probing.
2. Attach to the first running TIA Portal process when possible.
3. If `ProjectPath` is provided and no matching project is open, create a headless TIA Portal instance and open the project.
4. Find the requested device, PLC software, and target block folder.
5. Import the XML block file into the target folder.
6. Return a typed `SiemensPushResult` instead of crashing the app.

The adapter intentionally avoids compile-time references to Siemens assemblies. If Siemens changes method signatures between versions, keep the main abstraction and replace this implementation with either:

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
