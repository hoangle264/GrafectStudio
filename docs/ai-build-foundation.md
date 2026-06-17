# AI Build Foundation

## TypeScript Build
- TypeScript sources compile from `src/web/ts` to `src/web/js` via `npm run build:ts` / `npm run build`.
- `tsconfig.json` uses `module: "none"`, so generated files remain global scripts for the current WebView runtime.
- The AI foundation source is `src/web/ts/ai/bridge.ts` and the generated output is `src/web/js/ai/bridge.js`.

## Bridge Surface
- Legacy JavaScript should call AI foundation APIs through `window.GrafcetStudioAI`.
- The same API is also registered at `window.GrafcetStudio.ai` for consistency with existing compiled TypeScript bridges.
- P0 exposes only build/bridge metadata; AI types, sanitizers, context building, services, parsing, apply behavior, and UI are deferred to later phases.

## Runtime Ownership
- Compiled/generated AI module: `src/web/js/ai/bridge.js` from `src/web/ts/ai/bridge.ts`.
- Existing migrated TypeScript modules under `src/web/ts/core`, `src/web/ts/editor`, `src/web/ts/codegen`, and `src/web/ts/types` continue to generate matching files under `src/web/js`.
- UI/runtime files such as `src/web/js/editor/*-ui.js`, canvas/event/action/project scripts, and codegen modal scripts remain plain JavaScript.
