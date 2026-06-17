# AI API Integration Plan

## Summary
- Create a dedicated `Plan_AI.md` at the repository root for the AI API feature plan.
- Use Markdown checkboxes like `plan.md` so implementation progress can be tracked phase by phase.
- Keep the AI rollout safe and incremental: runtime schema validation, whitelist sanitization, mock/non-streaming before streaming, and dry-run apply behavior.
- Do not modify `plan.md` or `WORKPLAN.md`; those remain dedicated to their existing plans.

## Status Legend
- `[ ]` Pending
- `[~]` In progress
- `[x]` Done
- `[!]` Blocked

## Rules
- Never send the raw project object to any AI service.
- Never expose API keys in frontend JavaScript, WebView localStorage, prompts, or logs.
- AI responses must produce proposals only; they must not directly mutate project state.
- Users must preview every proposal before applying it.
- Runtime validation is required before rendering, previewing, or applying an AI proposal.
- Prefer small, independently verifiable phases over large cross-cutting changes.
- Streaming is not part of the first MVP; implement it only after the non-streaming proposal flow is stable.

## Dependency Order
```text
P0 Ã¢â‚¬â€ AI Build Foundation
 Ã¢â€ â€œ
P1 Ã¢â‚¬â€ Shared Types + Runtime Schemas
 Ã¢â€ â€œ
P2 Ã¢â‚¬â€ Sanitization Layer
 Ã¢â€ â€œ
P3 Ã¢â‚¬â€ Context Builder
 Ã¢â€ â€œ
P4 Ã¢â‚¬â€ Mock AI Service
 Ã¢â€ â€œ
P5 Ã¢â‚¬â€ Proposal Parser + Validator
 Ã¢â€ â€œ
P6 Ã¢â‚¬â€ Apply Layer + Dry Run
 Ã¢â€ â€œ
P7 Ã¢â‚¬â€ Chat UI Preview
 Ã¢â€ â€œ
P8 Ã¢â‚¬â€ Gemini Non-Streaming
 Ã¢â€ â€œ
P9 Ã¢â‚¬â€ Streaming Support
 Ã¢â€ â€œ
P10 Ã¢â‚¬â€ Integration + Security Tests
```

## AI Implementation Tasks

### P0 Ã¢â‚¬â€ AI Build Foundation
- [x] Verify the current TypeScript/esbuild output path and build scripts.
- [x] Keep the existing global-script WebView runtime model intact.
- [x] Define a stable frontend bridge surface, defaulting to `window.GrafcetStudioAI`.
- [x] Ensure legacy JavaScript can call compiled TypeScript AI functions through the bridge.
- [x] Document which AI modules are compiled/generated and which UI modules remain plain JavaScript.

#### Notes
- The AI layer should follow the existing TypeScript migration direction and avoid changing unrelated WebView loading behavior.
- P0 completed: current build uses `tsc` (`src/web/ts` -> `src/web/js`, `module: none`); AI bridge is exposed as `window.GrafcetStudioAI` and `window.GrafcetStudio.ai`; module ownership is documented in `docs/ai-build-foundation.md`.

### P1 Ã¢â‚¬â€ Shared Types + Runtime Schemas
- [x] Define `AiIntent` for `create-variable`, `clone-variable`, `map-io`, and `create-flow`.
- [x] Define `AiRequest`, `AiContext`, `AiProposal`, `AiProposalStatus`, and `ApplyResult` types.
- [x] Add runtime validators or type guards for every request and proposal shape.
- [x] Add a `schemaVersion` field to AI request/proposal contracts.
- [x] Cross-check proposal data fields against existing project and C# payload models to avoid drift.

#### Notes
- TypeScript types alone are not enough because AI responses are runtime data.
- Validators can be implemented with a small in-house guard layer or a dependency such as Zod/Valibot if the project accepts one.
- P1 completed: added `src/web/ts/ai/contracts.ts` with schemaVersion `1.0.0`, shared AI request/proposal/apply types, and dependency-free runtime guards aligned to existing project/C# payload field names (`DeviceVariable`, IO mapping entries, flow steps/transitions/connections).

### P2 Ã¢â‚¬â€ Sanitization Layer
- [x] Implement whitelist sanitizers for `variable`, `unit`, `diagram`, `step`, and `io` scopes.
- [x] Ensure sanitizers construct new context objects instead of deleting fields from raw project objects.
- [x] Remove or exclude file paths, machine names, template paths, local config, secrets, and host-specific metadata.
- [x] Add negative test fixtures containing path-like and secret-like fields.
- [x] Verify sanitized output contains only fields needed for the selected intent.

#### Notes
- Sanitization must be whitelist-first. Blacklist-only filtering is not acceptable for AI context.
- P2 completed: added whitelist-first `GrafcetStudioAISanitizer` for variable/unit/diagram/step/io scopes, returning newly constructed context objects and exposing only selected-intent scopes. Added `runSanitizerValidation()` self-contained negative fixture helper for path-like, machine, template, local config, and secret-like fields; validated with TypeScript typecheck/build and the helper.

### P3 Ã¢â‚¬â€ Context Builder
- [x] Implement context building from `message + selectedIntent + scope`.
- [x] Use selected intent from the UI or caller for the MVP.
- [x] Keep automatic intent detection optional and defer full reliance on it until after MVP.
- [x] Add context/token budget limits so large projects do not send excessive data.
- [x] Assemble a complete `AiRequest` only after sanitization and runtime validation pass.

#### Notes
- `detectIntent(message)` can exist as a helper, but it must not be the only supported path for the first version.
- P3 completed: added `GrafcetStudioAIContextBuilder.buildAiRequest()` and optional `detectIntent()`, using caller-selected intent by default, sanitizer-scoped context construction, message/context budget limits, explicit error results for invalid input, and runtime `AiRequest` validation before returning a request.

### P4 Ã¢â‚¬â€ Mock AI Service
- [ ] Add mock AI responses using the real `AiProposal` schema.
- [ ] Provide fixtures for each target intent, starting with `create-variable`.
- [ ] Route the frontend/C# integration through the same contract planned for the real AI service.
- [ ] Use the mock path to test parser, preview, and apply behavior without network or API costs.
- [ ] Add error fixtures for malformed JSON and wrong proposal shapes.

#### Notes
- Mock service comes before Gemini so UI, parser, and apply bugs can be isolated from API behavior.

### P5 Ã¢â‚¬â€ Proposal Parser + Validator
- [x] Extract structured JSON from AI responses.
- [x] Parse JSON and report malformed input as an error proposal/result.
- [x] Validate parsed proposals by `schemaVersion` and `intent`.
- [ ] Normalize valid proposals into the app's internal `AiProposal` shape.
- [x] Reject missing fields, wrong intent shapes, unknown intent values, and unsupported schema versions.

#### Notes
- Invalid AI output must never reach the apply layer as if it were valid data.
- P5 completed: added `GrafcetStudioAIProposalParser` with safe JSON extraction, explicit `{ ok, value/errors }` parse results, P1 `validateAiProposal()` validation, status normalization to `validated`, mock-service parser reuse, and parser validation fixtures for valid, malformed JSON, missing fields, wrong intent shape, unknown intent, and unsupported schema version.

### P6 Ã¢â‚¬â€ Apply Layer + Dry Run
- [x] Implement `dryRun` validation before any mutation.
- [x] Return an explicit `ApplyResult` with success, warnings, errors, changed state, and affected IDs.
- [x] Implement `create-variable` apply behavior first.
- [x] Add precondition checks for duplicates, missing source objects, invalid IO targets, and incompatible graph changes.
- [x] Prevent double-apply by tracking proposal status or applied proposal IDs.
- [x] Trigger `saveProject()`, `renderTree()`, `renderGlobalVarTable()`, and other refresh functions only after successful apply.

#### Notes
- Apply logic must be deterministic and independent from prompt text.
- P6 completed: added deterministic `GrafcetStudioAIApply` dry-run/apply API for validated `AiProposal` objects, create-variable mutation after full preflight, duplicate/source/IO/graph precondition checks, double-apply tracking, post-success save/render refresh, and self-contained apply validation fixtures.

### P7 Ã¢â‚¬â€ Chat UI Preview
- [x] Build plain JavaScript chat bubble rendering for AI messages and proposal previews.
- [x] Render preview content by proposal type, starting with variable proposals.
- [x] Add Apply, Discard, and Edit actions.
- [x] Update proposal status after user actions.
- [x] Call the compiled TypeScript AI bridge through a minimal API surface.
- [x] Handle validation and apply errors inside the bubble without crashing the editor.

#### Notes
- UI can remain plain JavaScript, but business logic should live behind the AI bridge where possible.
- P7 completed: added right-panel AI Preview chat bubbles, variable proposal preview rendering, Apply/Discard/Edit actions with status updates, bridge-backed parser/dry-run/apply calls, guarded Apply state, and in-bubble validation/apply error rendering.

### P8 Ã¢â‚¬â€ Gemini Non-Streaming
- [x] Implement the C# Gemini AI service behind an interface that can also support mock mode.
- [x] Store Gemini API keys only in host-side config, environment variables, or a secure user setting.
- [x] Build system prompts per intent with explicit JSON output requirements.
- [x] Send sanitized `AiRequest` data only.
- [x] Receive a full non-streaming response first and pass it through the parser/validator pipeline.
- [x] Avoid logging API keys, raw secrets, or full sensitive prompts.

#### Notes
- Non-streaming is the first real API milestone because it is easier to validate and debug than chunked JSON.
- P8 completed: added host-side `IAiCompletionService` with mock and Gemini implementations, environment-only Gemini config (`GRAFCETSTUDIO_AI_MODE=gemini`, `GEMINI_API_KEY`/`GRAFCETSTUDIO_GEMINI_API_KEY`, optional `GRAFCETSTUDIO_GEMINI_MODEL`), intent-specific JSON prompts, host-side `AiRequest` sanitization, full non-streaming response handoff to the existing parser/validator/preview path, and no secret/full-prompt logging. Validated C# build, TS typecheck/build, and mock mode; real Gemini request was not run because no API key was present in the environment.


### P9 Ã¢â‚¬â€ Streaming Support
- [x] Add streaming only after the non-streaming flow is stable.
- [x] Stream assistant text/status updates separately from the final structured proposal when possible.
- [x] Accumulate final JSON safely and validate it with the same proposal validator.
- [x] Handle partial chunks, canceled requests, timeout, malformed final JSON, and service errors.
- [x] Keep Apply disabled until a complete validated proposal exists.

#### Notes
- Streaming improves perceived responsiveness but must not bypass schema validation or user preview.
- P9 completed: added host-side streaming abstraction (`IAiCompletionService.StreamAsync`) with mock and Gemini SSE implementations, WebView stream events for status/delta/final/error/end, UI accumulation that keeps partial chunks separate from final proposal validation, and mock fixtures for partial JSON, cancel, timeout, malformed JSON, and service errors. Final structured proposals still pass through the existing parser/validator/preview path and Apply remains disabled until a complete validated proposal exists. Validated C# build, TS typecheck/build, JS streaming validation, and host mock streaming; real Gemini streaming was not run because no API key was present in the environment.


### P10 Ã¢â‚¬â€ Integration + Security Tests
- [x] Run TypeScript typecheck/build after each TS/JS-related phase.
- [x] Test sanitizer fixtures for file paths, machine names, template paths, secret-like fields, and oversized context.
- [x] Test parser/validator fixtures for valid proposal, malformed JSON, missing fields, wrong intent shape, and unsupported schema version.
- [x] Test apply behavior for dry-run, successful apply, failed preconditions, double-click Apply, edit then apply, and discard without mutation.
- [x] Test mock end-to-end flow before using the real Gemini API.
- [x] Test real Gemini non-streaming output only after mock parser/preview/apply flow passes.
- [x] Test full flow: JS chat UI Ã¢â€ â€™ context builder Ã¢â€ â€™ C# service Ã¢â€ â€™ parser Ã¢â€ â€™ preview Ã¢â€ â€™ apply Ã¢â€ â€™ save/render.

#### Notes
- Real API tests should be treated as integration checks, not the primary safety net.
- P10 completed: added bridge-level P10 validation for sanitizer/parser/apply/mock/full-flow checks, exported AI chat UI + streaming validation helpers, added host-side recursive request scrubbing validation, and validated with `npm.cmd run typecheck`, `npm.cmd run build`, Node P10 runtime validation, Node AI chat UI/streaming validation, and `dotnet build src\GrafcetStudio.App\GrafcetStudio.App.csproj -v:minimal`. Real Gemini non-streaming was skipped because no API key/network-enabled integration environment was available; mock parser/preview/apply flow passed first.

## Intent Priority
1. `create-variable` Ã¢â‚¬â€ simplest proposal shape, easy preview, easy validation, and safe apply behavior.
2. `clone-variable` Ã¢â‚¬â€ similar to create variable, but requires source variable validation and naming/offset rules.
3. `map-io` Ã¢â‚¬â€ depends on stronger validation because incorrect IO mapping can corrupt project logic.
4. `create-flow` Ã¢â‚¬â€ most complex; start with flow skeleton proposals before generating complete step/transition graphs.

## Assumptions
- `Plan_AI.md` lives at the repository root.
- `plan.md` remains the JavaScript to TypeScript migration plan.
- `WORKPLAN.md` remains the broader product/work plan.
- MVP uses selected intent from the UI or caller instead of relying fully on AI intent detection.
- Gemini API keys never appear in frontend JavaScript, localStorage, prompt logs, or committed files.
- Streaming is deferred until the non-streaming proposal workflow is implemented and verified.

