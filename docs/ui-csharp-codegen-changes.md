# UI Changes For C# Codegen Demo

This note records the UI-facing changes made to connect the WebView code generation modal to the C# generators.

## Changed File

- `src/web/js/codegen/modal.js`

## Changes

- Added two target options in the code generation modal:
  - `C# Keyence KV demo`
  - `C# TwinCAT ST demo`
- Add function modal.js
  - `cgBuildCSharpPayload`
- Added a WebView host request path using `window.chrome.webview.postMessage`.
- Added payload mapping from the current diagram state into the C# `CodegenPayload` shape:
  - `project`
  - `diagram`
  - `steps`
  - `transitions`
  - `variables`
- Added transition mapping from canvas `connections` into:
  - `fromStepIds`
  - `toStepIds`
- Added host callback handlers:
  - `receiveGeneratedCode(code)`
  - `receiveError(payload)`
- Added a host-unavailable message for browser-only preview.

## Related C# Wiring

- Disabled `MockCodeGeneratorService` registration and resolution so mock output no longer overwrites real C# output.
- Made the WebView bridge tolerate array/object payload fields for `GENERATE_CODE`.
- Made `CodegenPayload` deserialization case-insensitive.
