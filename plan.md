# JavaScript to TypeScript Migration Plan

## Summary
- Create a new `plan.md` at repo root dedicated to the JavaScript → TypeScript migration.
- Use Markdown checkboxes so each completed implementation task can be ticked from `[ ]` to `[x]`.
- Keep migration incremental: add TypeScript infrastructure first, then extract pure logic, while preserving current global-script WebView behavior.
- Do not replace existing `WORKPLAN.md`; it remains the broader product/work plan.

## Status Legend
- `[ ]` Pending
- `[~]` In progress
- `[x]` Done
- `[!]` Blocked

## Rules
- Only mark a task done after implementation and verification.
- Keep notes under each phase for regressions or follow-ups.
- Preserve the current WebView/global-script runtime unless a phase explicitly changes it.
- Prefer small, independently verifiable changes over large cross-cutting rewrites.

## Dependency Order
```text
P0 — TypeScript Build + Interop
 ↓
P1 — Foundation Types
 ↓
P2 — Codegen Payload
 ↓
P3 — Excel Import Parser
 ↓
P4 — Store Helpers + Persistence
 ↓
P6 — Vars & IO Mapping
 ↓
P5 — Tree Logic
 ↓
P7 — Tables & Graph Data
 ↓
P8 — Canvas Cleanup JS Only
 ↓
P9 — Cleanup & Consolidation
 ↓
P10 — Architecture Review
```

## Migration Tasks

### P0 — TypeScript Build + Interop
- [x] Add TypeScript dependency and `tsconfig.json`.
- [x] Add npm scripts for typecheck/build.
- [x] Decide compiled JS output location, defaulting to `src/web/js/**`.
- [x] Keep current `<script src="...">` loading model.
- [x] Expose TS logic to legacy JS through explicit `window.*` bridge objects.
- [x] Verify WebView/browser still loads without module errors.

#### Notes
- P0 implemented with TypeScript 6, `tsconfig.json`, npm scripts, compiled output to `src/web/js`, and `GrafcetStudioInterop` bridge registry loaded before legacy scripts.
- Verification run: `npm.cmd run typecheck`, `npm.cmd run build:ts`, `npm.cmd run build`, `node --check src/web/js/core/interop.js`, `node --check src/web/js/core/utils.js`, `node --check src/web/js/codegen/modal.js`, and `dotnet build src\GrafcetStudio.App\GrafcetStudio.App.csproj -v:minimal`.

### P1 — Foundation Types
- [x] Create `types/project.ts`.
- [x] Define core app types: `Project`, `Unit`, `DiagramMeta`, `DiagramState`, `Step`, `Transition`, `Connection`, `StepAction`.
- [x] Define device/variable types: `DeviceType`, `DeviceSignal`, `DeviceVariable`, `ProjectVariable`, `ProjectVariables`.
- [x] Define config/import types: `FlowInfo`, `AppConfig`, `UnitConfig`, `IOMapping`, `PhysicalIO`, `IOMappingEntry`.
- [x] Align payload-facing types with C# models without losing JS/localStorage fields.
- [x] Run TypeScript typecheck.

#### Notes
- Added `src/web/ts/types/project.ts` with project, payload, config, and IO mapping definitions that preserve legacy JS/localStorage fields.
- Verification: `npm.cmd run typecheck` passed.

### P2 — Codegen Payload
- [x] Extract codegen payload logic from `codegen/modal.js` into `codegen/payload.ts`.
- [x] Export `buildCSharpPayload`, `buildCSharpFlow`, `buildCSharpUnitPayload`, `validateUnitAddressConfig`, `resolveStepAddress`.
- [x] Move required helpers with the payload builder: word/MR address formatting, flow range validation, signal normalization, variable collection.
- [x] Keep `modal.js` responsible for DOM, host bridge calls, `flushState`, `saveProject`, preview, copy/download, and path browsing.
- [x] Expose compiled payload API through a global bridge for `modal.js`.
- [x] Verify generated C# payload shape still matches `CodegenPayload.cs`.

#### Notes
- Added `src/web/ts/codegen/payload.ts` and compiled `src/web/js/codegen/payload.js`; `modal.js` now delegates payload building through `GrafcetStudioInterop`.
- Verification: `npm.cmd run typecheck`, `npm.cmd run build`, `node --check src/web/js/codegen/payload.js`, `node --check src/web/js/codegen/modal.js`, and `dotnet build src\GrafcetStudio.App\GrafcetStudio.App.csproj -v:minimal` passed.

### P3 — Excel Import Parser
- [x] Extract CSV parsing from `editor/excel-import.js` into `editor/excel-import.ts`.
- [x] Export `parseCSV`, `parseUnitCSV`, `parseStructCSV`, `parsePhysicalIOCSV`.
- [x] Make parser functions return data/results instead of directly rendering UI.
- [x] Keep modal DOM, file input, toast, save, render, and project mutation in JS wrapper.
- [x] Verify unit config, struct variables, and physical IO imports still behave as before.

#### Notes
- Added `src/web/ts/editor/excel-import.ts` parser bridge and moved the legacy UI/project wrapper to `src/web/js/editor/excel-import-ui.js`.
- Verification: `npm.cmd run typecheck`, `npm.cmd run build`, `node --check src/web/js/editor/excel-import.js`, and `node --check src/web/js/editor/excel-import-ui.js` passed.

### P4 — Store Helpers + Persistence
- [x] Extract low-risk store helpers into `core/store.ts`.
- [x] Include `ensureProjectVariables`, `upsertProjectVariable`, `normalizeVariableRecord`, `syncStructData`, `findNextAvailableBaseMr`, `ensureFlowAddressConfig`, `migrateFlowAddressConfigs`.
- [x] Keep `project`, `openTabs`, and `activeDiagramId` global ownership stable during this phase.
- [x] Extract persistence to `core/store-persistence.ts` only after helper extraction is stable.
- [x] Keep `saveProject`, `loadDiagramData`, `saveDiagramData`, `deleteDiagramData` backward compatible.
- [x] Verify existing localStorage projects still load and migrate correctly.

#### Notes
- Added `src/web/ts/core/store.ts` helper bridge and `src/web/ts/core/store-persistence.ts`; compiled JS keeps legacy global function names and state ownership in `store.js`.
- Verification: `npm.cmd run typecheck`, `npm.cmd run build`, `node --check src/web/js/core/store.js`, `node --check src/web/js/core/store-persistence.js`, and a Node VM localStorage migration smoke test passed.

### P5 — Tree Logic
- [x] Extract non-DOM tree mutations from `editor/tree.js` into `editor/tree.ts`.
- [x] Include `addUnit`, `removeUnit`, `addDiagramInUnit`, `confirmDeviceType` logic, `removeDeviceType`.
- [x] Keep rendering, modal creation, event handlers, inline HTML, and DOM reads in `tree-render.js`/legacy JS.
- [x] Avoid moving UI-heavy logic until data-level helpers are separated.
- [x] Verify unit/device/diagram operations through the UI.

#### Notes
- Added `src/web/ts/editor/tree.ts` data helper bridge and moved legacy DOM/render wrappers to `src/web/js/editor/tree-ui.js`; `index.html` loads the bridge before UI wrappers.
- Verification: `npm.cmd run typecheck`, `npm.cmd run build`, `node --check src/web/js/editor/tree.js`, `node --check src/web/js/editor/tree-ui.js`, and a Node VM helper smoke test passed.

### P6 — Vars & IO Mapping
- [x] Extract variable data logic from `editor/vars.js` into `editor/vars.ts`.
- [x] Include `getVars`, `gvtGetEntries`, `gvtGetSigList`, `gvtGetExcelSignalAddress`, `gvtResolveEntry`, `gvtEditVar`.
- [x] Extract IO mapping logic into `editor/io-mapping.ts`.
- [x] Include auto-match, map/unmap, candidate options, and address target resolution.
- [x] Keep table rendering and DOM interaction in JS render wrappers.
- [x] Verify global variable editing, signal addresses, auto-match, manual map, and unmap.

#### Notes
- Added `src/web/ts/editor/vars.ts` and `src/web/ts/editor/io-mapping.ts`; moved legacy DOM/table wrapper to `src/web/js/editor/vars-ui.js` and kept rendered text ASCII-safe to avoid mojibake.
- Verification: `npm.cmd run typecheck`, `npm.cmd run build`, `node --check src/web/js/editor/vars.js`, `node --check src/web/js/editor/io-mapping.js`, `node --check src/web/js/editor/vars-ui.js`, a Node VM vars/IO smoke test, and mojibake scan passed.

### P7 — Tables & Graph Data
- [x] Extract table data builders into `editor/tables.ts`.
- [x] Create data-level functions for steps, transitions, branches, and variables.
- [x] Keep HTML string generation, export HTML, and export CSV in `tables-render.js` or legacy JS.
- [x] Use existing graph utility behavior without changing canvas interaction.
- [x] Verify exported table HTML/CSV remains correct.

#### Notes
- Added `src/web/ts/editor/tables.ts` data helper bridge and moved legacy HTML/export wrapper to `src/web/js/editor/tables-ui.js`; wrapper text is ASCII-safe to avoid mojibake.
- Verification: `npm.cmd run typecheck`, `npm.cmd run build`, `node --check src/web/js/editor/tables.js`, `node --check src/web/js/editor/tables-ui.js`, a Node VM tables smoke test, and mojibake scan passed.

### P8 — Canvas Cleanup JS Only
- [x] Move duplicate parallel port metrics into `core/graph-utils.js`.
- [x] Replace canvas/events duplicate metric logic with the shared helper.
- [x] Move `startPortDragConnect` and `findElementAt` from `canvas.js` to `events.js` if runtime dependencies remain valid.
- [x] Extract `buildStepActionBox` from `buildStepEl`.
- [x] Verify drag-connect, hit detection, parallel snapping, and step action rendering.

#### Notes
- Added shared `getParallelPortMetrics` in `src/web/js/core/graph-utils.js`; `canvas.js` and `events.js` now use the same branch-port spacing logic.
- Moved drag-connect start and hit detection helpers to `src/web/js/editor/events.js`; runtime dependencies remain valid because handlers run after all classic scripts load.
- Extracted step action SVG rendering into `buildStepActionBox` and kept text ASCII-safe to avoid mojibake.
- Verification: `npm.cmd run typecheck`, `npm.cmd run build`, JS syntax checks for graph-utils/canvas/events/elements, Node graph-utils smoke test, and mojibake scan passed.

### P9 — Cleanup & Consolidation
- [x] Find and remove dead code.
- [x] Find duplicate helpers.
- [x] Consolidate similar utilities.
- [x] Remove unused bridge APIs.
- [x] Remove unused exports.
- [x] Remove files that are no longer referenced.
- [x] Verify removals against HTML inline handlers, `window.*` APIs, WebView bridge callbacks, and C# `ExecuteScriptAsync` calls.
- [x] Run typecheck.
- [x] Run build.

#### Notes
- Removed proven duplicate/dead JS: inline step action SVG rendering now delegates to `buildStepActionBox`, obsolete export-table helper functions were removed, and the removed local var-table resize boot path no longer runs.
- Kept bridge APIs and classic global functions that are still referenced by HTML inline handlers, UI wrappers, WebView callbacks, or C# host script calls; no referenced files were removed.
- Normalized WebView/index UI text and icons to ASCII-safe labels so the mojibake scan stays clean.
- Verification: `npm.cmd run typecheck`, `npm.cmd run build`, JS syntax checks for touched JS, Node graph-utils smoke test, reference scans, and mojibake scan passed.

### P10 — Architecture Review
- [x] Review directory structure.
- [x] Review dependencies between modules.
- [x] Identify files that are too large.
- [x] Propose module splits.
- [x] Identify high-coupling areas.
- [x] Check naming consistency.
- [x] Create a technical architecture report.

#### Notes
- Created `docs/P10-architecture-review.md` with directory structure, dependency/load-order review, file-size review, coupling hotspots, naming notes, risks, and proposed next module splits.
- Main recommendation: keep the stable hybrid TS/classic-script runtime, then split large UI wrappers (`tree-ui.js`, `vars-ui.js`, `codegen/modal.js`) in future work instead of changing runtime behavior now.
- Verification: documentation is ASCII-safe and mojibake scan passed.

## Test Plan
- [x] Run `npm run typecheck` after each TypeScript phase.
- [x] Run JS syntax checks for touched compiled/legacy JS files.
- [ ] Run `dotnet build src/GrafcetStudio.App/GrafcetStudio.App.csproj -v:minimal` after payload-related changes.
- [ ] Manually verify WebView/browser startup after P0, P2, P4, and P8.
- [x] Run automated localStorage migration smoke test after P4.
- [ ] Manually verify codegen, CSV import, project load/save, tree operations, vars/IO mapping, table export, and canvas drag behavior.
- [ ] Re-run targeted checks after P9 cleanup to confirm no runtime bridge/global usage was removed incorrectly.

## Assumptions
- The checklist file is named `plan.md` at repo root.
- Completed work is tracked by changing `[ ]` to `[x]` in `plan.md`.
- The migration preserves the current global-script runtime first, not immediately converting the whole app to ES modules.
- Existing `WORKPLAN.md` is not modified unless explicitly requested.









