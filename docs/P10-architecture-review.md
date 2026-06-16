# Architecture Review - P10

## Scope
- Review the WebView JS runtime after the TypeScript migration phases P0-P9.
- Focus on directory structure, module dependencies, file size, coupling, and naming consistency.
- Do not propose behavior changes for the canvas/runtime in this phase.

## Directory Structure

### `src/web/ts`
- Holds the extracted TypeScript source of record for logic that was moved out of legacy JS.
- Current split is clear and bounded:
  - `core/` for interop, store helpers, and persistence helpers.
  - `codegen/` for payload generation.
  - `editor/` for data-layer logic extracted from UI-heavy files.
  - `types/` for shared project types.
- The TS tree is easier to reason about than the legacy JS tree because each bridge owns one domain.

### `src/web/js`
- Still the runtime entrypoint for the WebView classic-script model.
- Contains both legacy UI wrappers and compiled TS output.
- The folder is now the main coupling point between the migration layers because classic JS still owns global state and DOM wiring.

### `src/GrafcetStudio.App`
- The C# host is thin and mostly forwards WebView messages and bridge calls.
- `MainWindow.xaml.cs` and `WebViewBridgeService.cs` define the browser-host boundary.
- This layer should stay narrow; most product logic belongs in JS/TS or dedicated services.

## Dependency Review

### Load Order
The page still depends on classic script order in `src/web/index.html`:
1. Core bridge and utility scripts.
2. Shared graph/core constants.
3. Editor logic and wrappers.
4. Codegen and import wrappers.

This order is important because many files still publish globals with `Object.assign(window, ...)` or rely on globals declared by earlier scripts.

### Bridge Boundaries
- `GrafcetStudioInterop.registerBridge(...)` is the main TS-to-JS bridge pattern.
- Current bridges are domain-specific: `store`, `tree`, `vars`, `ioMapping`, `tables`, `excelImport`, and `codegenPayload`.
- The browser wrapper side still depends on these bridges through `window.GrafcetStudio.*`.
- The C# host side depends on `ExecuteScriptAsync` callback names such as `receiveGeneratedCode`, `receiveError`, `loadProjectData`, and related message handlers.

### Coupling Hotspots
- `src/web/js/editor/events.js` is still a central interaction hub and knows about canvas state, selection, drag, connect, snapping, resize, and context menu behavior.
- `src/web/js/editor/canvas.js` is tightly coupled to SVG rendering helpers and element geometry.
- `src/web/js/editor/project.js` owns global project navigation and layout/view state.
- `src/web/js/editor/tree-ui.js` and `src/web/js/editor/vars-ui.js` are large wrapper files because they combine DOM rendering, event handlers, and bridge usage.
- `src/web/js/codegen/modal.js` remains large because it owns the modal UI plus host bridge orchestration.

## File Size Review
Largest files after the migration are still the expected UI-heavy ones:
- `src/web/css/grafcet-studio.css`
- `src/web/js/editor/tree-ui.js`
- `src/web/index.html`
- `src/web/js/editor/events.js`
- `src/web/js/editor/vars-ui.js`
- `src/web/js/codegen/modal.js`
- `src/web/ts/core/store.ts`
- `src/web/js/editor/canvas.js`
- `src/web/js/editor/project.js`
- `src/web/js/editor/excel-import-ui.js`

Interpretation:
- The largest files are mostly UI wrappers, which is acceptable for this phase.
- The extracted TS files are reasonably sized and domain-focused.
- `tree-ui.js` and `vars-ui.js` are the most obvious candidates for future split work if P10+ is expanded.

## Naming Consistency

### Good Patterns
- `*-ui.js` cleanly identifies DOM-heavy wrappers.
- `core/`, `editor/`, and `codegen/` directories are aligned with responsibility.
- Bridge names such as `codegenPayload`, `ioMapping`, and `store` are descriptive and stable.

### Inconsistencies / Follow-ups
- Some wrapper files still expose a broad mix of helpers with older global naming style.
- Several legacy JS comments and helper names are still terse because they predate the migration.
- `tree-ui.js` and `vars-ui.js` could benefit from more explicit internal helper grouping if they are split further.

## High-Coupling Areas
- `events.js` depends on many globals and is the highest-risk file for future refactors.
- `canvas.js` depends on shared geometry constants, selection state, and SVG helpers.
- `project.js` depends on tab state, localStorage persistence, and layout state.
- `codegen/modal.js` depends on host callbacks, saved paths, payload bridges, and modal DOM state.
- `index.html` is still the runtime composition root and defines the effective module order.

## Recommended Next Splits
1. Split `tree-ui.js` into smaller files:
   - tree rendering
   - device modal
   - diagram properties panel
   - tree context menu
2. Split `vars-ui.js` into smaller files:
   - global variable table
   - IO mapping table
   - variable table boot / search / resize behavior
3. Split `codegen/modal.js` into:
   - modal lifecycle and DOM
   - host bridge communication
   - preview/export helpers
4. Keep `events.js` unchanged unless a future phase can safely isolate drag/connect logic further.

## Risks
- Classic-script load order is still a hard dependency; reordering scripts can break globals silently.
- Removing a wrapper/global too early would break inline HTML handlers in `index.html`.
- The C# host is coupled to exact callback names, so bridge or callback renames need coordinated changes.
- Build output is emitted into `src/web/js`, so TS compile artifacts can overwrite hand-edited JS if a file becomes part of the TS build set.

## Conclusion
The migration has reached a stable hybrid architecture: TS now owns most extracted logic, while classic JS still handles DOM, inline handlers, and WebView lifecycle. The remaining technical debt is concentrated in a few large wrapper files and the event/canvas interaction layer. The next best architectural work is targeted wrapper splitting, not a runtime model rewrite.
