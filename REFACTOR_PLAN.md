# REFACTOR_PLAN.md — JS → TS migration (GrafcetStudio frontend)

> Kế hoạch tái cấu trúc frontend `src/web` từ JS sang TypeScript, giữ nguyên lõi canvas (Tier D).
> Build model **thực tế**: `tsc` thuần (`module:"none"`, `outDir: src/web/js`) → biên dịch 1 `.ts` → 1 `.js` **tại chỗ**, nạp qua 43 thẻ `<script>` theo thứ tự trong `index.html`. **KHÔNG có esbuild/bundler.** Ràng buộc = global namespace + thứ tự script.
> Trạng thái: 18/44 file đã là TS. Còn 25 file JS + việc consolidation.

---

## QUY TẮC BẮT BUỘC cho Sonnet/Codex khi làm theo file này

1. **Chỉ làm 1 Phase mỗi lần, không nhảy cóc.** Làm đúng thứ tự Phase 0 → 1 → 2 → …
2. **Trước khi bắt đầu bất kỳ Phase nào, đọc mục "Trạng thái hiện tại".** Nếu còn checkbox `[ ]` chưa tick trong đó → **DỪNG và hỏi người dùng**, không tự quyết.
3. **Sau khi xong 1 Phase:** tick checkbox tổng của Phase, ghi 1 dòng vào mục "Nhật ký", rồi **DỪNG chờ người dùng xác nhận** mới sang Phase kế tiếp. **Không tự động chạy liên tục qua nhiều Phase.**
4. **Không tự ý sửa nội dung Phase khác** ngoài Phase đang làm (không đổi input/output, tiêu chí, checklist của Phase khác).
5. **Mỗi Phase phải `npm run typecheck` (= `tsc --noEmit`) PASS** trước khi tick done.
6. **KHÔNG stage/commit file trong `src/web/js/**`** do `tsc` sinh ra lẫn với source `.ts` (build artifact — xem quyết định `.gitignore` ở "Trạng thái hiện tại").
7. **Mọi hàm gọi qua `onclick="..."` trong HTML string phải expose qua `window.xxx`** — tooling (knip/tsc) KHÔNG bắt được điểm mù này; đổi tên/scope làm chết nút bấm mà không báo lỗi.
8. **Không thêm dependency npm mới, không thêm esbuild/bundler, không đổi `module:"none"`.**

---

## Trạng thái hiện tại — QUYẾT ĐỊNH CẦN NGƯỜI DÙNG XÁC NHẬN

> Còn checkbox `[ ]` nào chưa tick ở đây → Sonnet/Codex DỪNG, hỏi người dùng trước khi chạy Phase.

- [x] **Q1 — `codegen/unit-config.js` đang có change chưa commit (git status `M`).** ĐÃ COMMIT (`51d5081`). Git status của file sạch.
- [x] **Q2 — `AppConfig` → `CodegenPayload`: rename cứng.** Người dùng chọn **rename cứng** — không giữ alias `AppConfig`.
- [x] **Q3 — `ActionQualifier`: strict union, bỏ `| string`.** Người dùng chọn **strict union** đúng enum C# (`N,S,R,P,P0,L,D,SD,DS,SL`), không mở `| string`.
- [x] **Q4 — `.gitignore` cho `src/web/js/**`: chọn A.** Người dùng chọn **[A]** — thêm `src/web/js/**` vào `.gitignore`, coi là artifact thuần.
- [x] **Q5 — Mức gộp nhóm modal (Phase 5): tối thiểu.** Người dùng xác nhận mức tối thiểu — gộp `modal-export`+`modal-assets` vào `modal.ts`, giữ `modal-host`/`modal-selectors` riêng.

---

## Phase 0 — Nền (types + tsconfig)

- [x] **PHASE 0 HOÀN TẤT**

**Input:** `tsconfig.json`, `src/web/ts/types/project.ts`, `src/web/ts/core/interop.ts`
**Output:** `types/project.ts` (đã đồng bộ DTO C#), không đổi `index.html`
**Phụ thuộc:** — (Phase gốc)

**Checklist con:**
- [x] Xác nhận build model = tsc namespace, KHÔNG đụng/không thêm esbuild.
- [x] Sửa `ActionQualifier` trong `types/project.ts`: bỏ `'P1'`, thêm `'SD','DS','SL'`, bỏ hẳn `| string` (strict union theo **Q3**). Đối chiếu đúng `Domain/Enums/ActionQualifier.cs`.
- [x] Sửa `Step`: bỏ trùng `isInitial`, giữ 1 field `initial` (khớp `Step.cs` → `JsonPropertyName("initial")`). Tách nhóm field canvas-only (`x,y,w,h,connections`) có comment.
- [x] Sửa `StepAction`: `qualifier: ActionQualifier` (bỏ `?`, C# non-null default N), `timeMs: number`; giữ `time?` với comment "UI-only, không serialize".
- [x] Thêm/đổi `CodegenPayload` khớp `CodegenPayload.cs`: đủ `platform, templateRootPath, project, unit, units, flows, variables, deviceTypes, deviceLibraryPath, templateProfile`. Rename cứng `AppConfig` → `CodegenPayload` theo **Q2**, đã sửa mọi ref trong `codegen/payload.ts`.
- [x] Gỡ `[key: string]: unknown` khỏi 5 type serialize-sang-C#: `Step`, `Transition`, `StepAction`, `DeviceVariable`, `CodegenPayload` (giữ index signature cho type render-canvas như `Project`, `Unit`, `DiagramMeta`...).
- [x] `grep -rn "P1\|AppConfig" src/web/ts` — sạch, không còn ref vỡ. Vá thêm: `ai/apply.ts` (`normalizeFlowAction` nhận `unknown`, trả `StepAction` đủ `qualifier`/`timeMs`), `ai/mock-service.ts` (thêm `timeMs: 0` cho action mock), `ai/sanitizer.ts` (4 chỗ `return result as unknown as X` do mất index signature), `editor/vars.ts` (`as unknown as Record<string, unknown>`).
- [x] `npm run typecheck` PASS.

**Tiêu chí done:** typecheck pass; 5 type serialize KHÔNG còn any ngầm (index signature); `index.html` KHÔNG đổi; DTO C# là nguồn sự thật, field map 1-1 `JsonPropertyName`.

**Rủi ro + rollback:**
- Sửa `ActionQualifier` vỡ chỗ gán `'P1'`/chuỗi tự do trong `ai/sanitizer.ts`, `codegen/payload.ts`. Test: `grep -rn "P1\|qualifier" src/web/ts` + typecheck. Rollback: `git checkout types/project.ts`; tạm dùng `ActionQualifier | (string & {})`.
- Rename `AppConfig`→`CodegenPayload` phá ref. Test: `grep -rn "AppConfig" src/web/ts`. Rollback: giữ `export type AppConfig = CodegenPayload;`.

---

## Phase 1 — core thuần

- [x] **PHASE 1 HOÀN TẤT**

**Input:** `src/web/js/core/constants.js` (42), `core/utils.js` (28), `core/graph-utils.js` (79)
**Output:** `src/web/ts/core/{constants,utils,graph-utils}.ts`
**Phụ thuộc:** Phase 0 (types)

**Checklist con:**
- [x] Chuyển `core/utils.js` → `core/utils.ts` (hàm thuần, không đụng canvas global).
- [x] Chuyển `core/constants.js` → `core/constants.ts` (chỉ hằng số hình học — 10 const nhóm 1; nhóm 2 biến runtime canvas KHÔNG đưa vào đây, xem ghi chú dưới).
- [x] Chuyển `core/graph-utils.js` → `core/graph-utils.ts`, dùng type `Step/Connection` từ `GrafcetStudioProject`; `ParallelBar` định nghĩa cục bộ (chưa có type dùng chung).
- [x] Không có `declare function` nào cho utils/graph-utils cần thay — không file TS nào tham chiếu `esc/esc2/show/hide/closeModal/toast/resolveStepsThrough/getParallelPortMetrics` trước Phase này.
- [x] Thứ tự thẻ `<script>` utils/graph-utils/constants trong `index.html` giữ nguyên; chỉ thêm 1 thẻ mới `js/editor/canvas-state.js` (xem ghi chú dưới).
- [x] Untrack 3 file `.js` build-artifact khỏi git (`git rm --cached`) + thêm vào `.gitignore` (phạm vi hẹp: chỉ 3 file, không phải `src/web/js/**` toàn bộ — xem Nhật ký).
- [x] `npm run typecheck` PASS.

**Tiêu chí done:** 3 file thành `.ts`; thứ tự script giữ nguyên cho 3 file gốc; typecheck pass; bridge không đổi.

**Rủi ro + rollback:**
- `utils.ts` emit `.js` khác chữ ký cũ → `events.js` (Tier D) gọi sai. Test: so diff `utils.js` trước/sau `tsc`, mở app thao tác canvas cơ bản. Rollback: `git checkout src/web/js/core/utils.js`; revert `.ts`.
- Thứ tự script: `constants.js`(#6) sau `store.js`(#5), nếu ref constant tại parse-time → ReferenceError. Test: load `index.html`, xem console. Rollback: giữ nguyên thứ tự #5/#6.

**Ghi chú phát sinh (ngoài dự kiến ban đầu của Phase, đã xác nhận với người dùng):**
- `constants.js` gốc (42 dòng) không chỉ chứa hằng số — có 2 nhóm: (1) 10 hằng số hình học thuần `SW,SH,TW,TH,PH,GRID,ACT_W,SNAP_ENTER_THRESHOLD,SNAP_EXIT_THRESHOLD,PAR_PORT_INSET,PAR_PORT_MIN_INSET,PAR_PORT_MIN_USABLE` → convert vào `constants.ts`; (2) biến trạng thái runtime canvas `state,nextId,nextStepNum,viewX,viewY,viewScale,snapOn,tool,selIds,dragging,dragMap,dragSnapState,dragSnapCandidates,dragSnapPrimaryId,panning,panSX,panSY,connecting,connFrom,selBoxing,selBoxSX,selBoxSY,resizingBar,resizeStartX,resizeStartW,ctxTarget,renameMode` — đây chính là loại global bị Tier D (`canvas.js,events.js,elements.js,project.js,actions.js`) đọc/ghi liên tục. Convert nhóm (2) sang `.ts` gây `TS2451: Cannot redeclare block-scoped variable` với `declare let state/nextId/.../viewScale` đã có sẵn trong `store.ts` (Phase 0). Người dùng chọn **phương án A**: tách nhóm (2) sang file JS mới `src/web/js/editor/canvas-state.js` (giữ nguyên JS, KHÔNG convert, KHÔNG đụng `store.ts`), ghi nợ kỹ thuật xử lý cùng đợt quyết định chiến lược Tier D sau này.
- `.gitignore`: Q4 gốc chốt ignore toàn bộ `src/web/js/**`, nhưng `canvas-state.js` (JS nguồn thật, không phải build artifact) cũng nằm trong thư mục này → nếu ignore cả thư mục sẽ không track được. Người dùng đồng ý thu hẹp phạm vi: chỉ ignore 3 file build-artifact cụ thể vừa convert (`src/web/js/core/{utils,constants,graph-utils}.js`), giữ track các file JS thật còn lại. Danh sách ignore sẽ tăng dần theo từng Phase.
- **Nợ kỹ thuật:** nhóm biến runtime canvas trong `constants.js` cũ nay nằm ở `src/web/js/editor/canvas-state.js`, chưa type hoá — sẽ xử lý cùng đợt với quyết định chiến lược Tier D (Phase D) sau này, không xử lý lẻ ở Phase 1.

---

## Phase 2 — Tier B logic/data

- [x] **PHASE 2 HOÀN TẤT**

**Input:** `src/web/js/editor/export.js` (345), `editor/change-manager.js` (16), `codegen/unit-config.js` (65 — **chỉ khi Q1 tick**)
**Output:** `src/web/ts/editor/{export,change-manager}.ts`, `src/web/ts/codegen/unit-config.ts`
**Phụ thuộc:** Phase 1; `unit-config` phụ thuộc **Q1** (git status sạch)

**Checklist con:**
- [x] Chuyển `editor/change-manager.js` → `.ts` (16 dòng dirty-state, không đụng canvas geometry).
- [x] Chuyển `editor/export.js` → `.ts`, ref `store`(TS) đúng type, dùng `CodegenPayload`/`Project` từ namespace.
- [x] **KIỂM TRA Q1**: `git diff src/web/js/codegen/unit-config.js` rỗng (Q1 đã tick từ trước) → chuyển `codegen/unit-config.js` → `.ts`.
- [x] Không tạo dependency mới lên canvas render-globals.
- [x] `index.html`: không cần sửa (đuôi tham chiếu `.js` không đổi tên).
- [x] `npm run typecheck` PASS.

**Tiêu chí done:** các file thành `.ts`; không đụng canvas globals mới; `store`(TS) ref đúng; typecheck pass; `unit-config` chỉ làm khi git sạch.

**Rủi ro + rollback:**
- `unit-config.js` có change chưa commit → convert nuốt/đè việc dở. Test: `git diff src/web/js/codegen/unit-config.js`. Rollback: KHÔNG bắt đầu tới khi Q1 tick; nếu lỡ, `git stash pop`.

---

## Phase 3 — Tier C: nhóm tree

- [ ] **PHASE 3 HOÀN TẤT**

**Input:** `src/web/js/editor/{tree-ui,tree-devices-ui,tree-diagrams-ui}.js`
**Output:** `src/web/ts/editor/{tree-ui,tree-devices-ui,tree-diagrams-ui}.ts`
**Phụ thuộc:** Phase 1 (logic `tree.ts` đã có sẵn)

**Checklist con:**
- [ ] `grep -rn 'onclick=' src/web/index.html` + trong 3 file → liệt kê mọi handler gọi qua onclick.
- [ ] Chuyển `tree-ui.js` → `.ts`; giữ nguyên mọi `window.xxx = xxx` cho handler onclick.
- [ ] Chuyển `tree-devices-ui.js` → `.ts`; DOM dùng type cụ thể (`HTMLElement`/`HTMLInputElement`…), không `any`.
- [ ] Chuyển `tree-diagrams-ui.js` → `.ts`; ref `tree.ts`, `store.ts` type đúng.
- [ ] Đối chiếu từng handler onclick sau convert vẫn expose qua `window.`.
- [ ] `npm run typecheck` PASS.

**Tiêu chí done:** 3 file thành `.ts`; mọi hàm onclick expose qua `window.xxx` giữ nguyên; DOM type cụ thể; typecheck pass; mở/đóng cây thủ công OK.

**Rủi ro + rollback:**
- Hàm onclick mất khi đổi tên/scope TS → nút chết im (tsc không bắt). Test: `grep -rn 'onclick=' src/web/index.html src/web/ts` đối chiếu `window.`; click thử từng nút. Rollback: revert file `.ts`; đảm bảo handler cũ vẫn `window.name = name`.

---

## Phase 4 — Tier C: nhóm form

- [ ] **PHASE 4 HOÀN TẤT**

**Input:** `src/web/js/editor/{vars-ui,vars-boot-ui,tables-ui,io-mapping-ui,excel-import-ui}.js`
**Output:** `src/web/ts/editor/{vars-ui,tables-ui,io-mapping-ui,excel-import-ui}.ts` (gộp `vars-boot-ui` vào `vars-ui`)
**Phụ thuộc:** Phase 1 (logic `vars.ts`,`tables.ts`,`io-mapping.ts`,`excel-import.ts` đã TS)

**Checklist con:**
- [ ] Chuyển `vars-ui.js` → `.ts`; **gộp 11 dòng `vars-boot-ui.js` vào `vars-ui.ts`** (boot-shim là bước đời của chính module).
- [ ] Xóa thẻ `<script>` của `vars-boot-ui.js` (#32) khỏi `index.html`; xóa file nguồn.
- [ ] Chuyển `tables-ui.js`, `io-mapping-ui.js`, `excel-import-ui.js` → `.ts`.
- [ ] Giữ nguyên mọi `window.*` handler onclick.
- [ ] Nếu `vars-ui.ts` > 300 dòng sau khi bỏ boot-shim VÀ có ranh giới rõ (form vs table) → cân nhắc tách; nếu không rõ ràng thì KHÔNG tách.
- [ ] `npm run typecheck` PASS.

**Tiêu chí done:** gộp `vars-boot-ui`→`vars-ui.ts` xong, đã xóa thẻ script tương ứng; các `window.*` handler còn nguyên; typecheck pass.

**Rủi ro + rollback:**
- Xóa thẻ script `vars-boot-ui` làm mất bước boot → vars-ui không khởi tạo. Test: mở tab vars, kiểm tra render. Rollback: khôi phục thẻ + file; revert `.ts`.
- Handler onclick mất (như Phase 3). Test/rollback tương tự Phase 3.

---

## Phase 5 — Tier C: modal + ai-chat

- [ ] **PHASE 5 HOÀN TẤT**

**Input:** `src/web/js/codegen/{modal-selectors,modal-host,modal-assets,modal-export,modal}.js`, `editor/ai-chat-ui.js`
**Output:** `src/web/ts/codegen/{modal,modal-selectors,modal-host}.ts` (gộp `modal-export`+`modal-assets`→`modal.ts` theo **Q5**), `src/web/ts/editor/ai-chat-ui.ts`
**Phụ thuộc:** Phase 2 (`payload.ts`,`unit-config.ts`), Phase 0

**Checklist con:**
- [ ] Xác nhận **Q5** đã chốt mức gộp.
- [ ] Chuyển `modal-selectors.js` → `.ts` (DOM query, type cụ thể).
- [ ] Chuyển `modal-host.js` → `.ts`; **KHÔNG đổi shape message** `window.chrome.webview.postMessage`/tên field JSON gửi C#.
- [ ] Gộp `modal-export.js` (12) + `modal-assets.js` (85) vào `modal.ts`; xóa 2 thẻ `<script>` tương ứng (#42, #43) khỏi `index.html`.
- [ ] Chuyển `modal.js` → `.ts`; các file modal dùng chung namespace nếu tách.
- [ ] Chuyển `ai-chat-ui.js` → `.ts`; **KHÔNG đổi behavior AI pipeline** (propose→confirm→apply), chỉ thêm type.
- [ ] Đối chiếu thứ tự khởi tạo modal (selectors chạy sau DOM ready) vẫn đúng.
- [ ] `npm run typecheck` PASS.

**Tiêu chí done:** gộp `modal-export`+`modal-assets`→`modal.ts`, đã xóa thẻ script tương ứng; bridge `modal-host`→C# giữ postMessage nguyên shape; AI pipeline behavior KHÔNG đổi; typecheck pass.

**Rủi ro + rollback:**
- Migrate `ai-chat-ui`/`modal-host` đổi payload `postMessage` (shape/tên field). Test: so diff JSON body trước/sau (log postMessage), host C# nhận đúng. Rollback: revert; giữ nguyên cấu trúc message.
- Gộp modal lệch thứ tự khởi tạo (selectors trước DOM ready). Test: mở modal codegen, generate thử. Rollback: tách lại theo `index.html` cũ.

---

## Phase 6 — Tier C: panels (điều kiện)

- [ ] **PHASE 6 HOÀN TẤT**

**Input:** `src/web/js/editor/panels.js` (89)
**Output:** `src/web/ts/editor/panels.ts` (CHỈ nếu chứng minh không đụng canvas geometry)
**Phụ thuộc:** Phase 0

**Checklist con:**
- [ ] Đọc `panels.js`, xác nhận chỉ toggle panel/DOM layout, KHÔNG đọc/ghi canvas geometry global (`state`, `viewX`, tọa độ vẽ).
- [ ] Nếu SẠCH → chuyển `panels.js` → `.ts`, giữ `window.*` handler.
- [ ] Nếu VƯỚNG canvas → **HOÃN**, ghi nợ vào Nhật ký, KHÔNG cố convert.
- [ ] `npm run typecheck` PASS (nếu có làm).

**Tiêu chí done:** `panels.ts` build pass VÀ không đụng canvas geometry; hoặc ghi nợ rõ ràng nếu hoãn.

**Rủi ro + rollback:** như Phase 3 (onclick handler). Nếu phát hiện coupling canvas → dừng, revert, giữ JS.

---

## Phase D — GIỮ JS (KHÔNG convert)

- [x] **KHÔNG có việc convert** — chỉ bảo trì `declare`

**Phạm vi giữ JS:** `editor/{canvas,events,elements,project,actions}.js`

**Ràng buộc:**
- KHÔNG convert 5 file này. KHÔNG tách `events.js` (731 dòng) dù lớn — ghi nợ kỹ thuật, ngoài phạm vi lần này.
- Chỉ đảm bảo mọi `declare let/function` phía TS khớp chữ ký runtime global do các file JS này khai báo (`state`, `nextId`, `viewX`, `viewY`, `viewScale`, `openTab`, `ucEnsureCylinderDeviceType`, addStep…).

**Tiêu chí done (kiểm tra định kỳ):** các file TS vẫn `declare` đúng runtime global; không có `declare` trỏ tới hàm đã bị xóa/đổi tên trong file JS.

---

## VIỆC KHÔNG LÀM LẦN NÀY (chốt phạm vi)

1. KHÔNG thêm esbuild/bundler/ES module. Giữ `module:"none"` + namespace toàn cục + thẻ `<script>`.
2. KHÔNG convert Tier D (`canvas,events,elements,project,actions`). KHÔNG tách `events.js`.
3. KHÔNG đổi behavior AI pipeline (`ai/*` đã TS) — chỉ sửa type, không sửa luồng.
4. KHÔNG đổi shape message bridge `window.chrome.webview.postMessage`/tên field JSON gửi C#.
5. KHÔNG hardcode tên thiết bị/địa chỉ PLC phía TS.
6. KHÔNG thêm dependency npm mới ngoài `typescript`.
7. KHÔNG đụng `codegen/unit-config.js` tới khi Q1 tick (git sạch).
8. KHÔNG rename ồ ạt biến/hàm public khi convert (rủi ro đứt onclick/window.*). Chỉ thêm type.
9. KHÔNG stage/commit `src/web/js/**` do tsc sinh lẫn với source `.ts`.

---

## Nhật ký (Sonnet/Codex tự ghi sau mỗi Phase)

| Ngày | Phase | typecheck | Vấn đề phát sinh / ghi chú | Người/Model làm |
|------|-------|-----------|----------------------------|-----------------|
| 2026-07-04 | 0 | PASS | Rename `AppConfig`→`CodegenPayload`; strict `ActionQualifier`; gỡ index signature khỏi Step/Transition/StepAction/DeviceVariable/CodegenPayload; vá 4 file phụ thuộc (`ai/apply.ts`, `ai/mock-service.ts`, `ai/sanitizer.ts`, `editor/vars.ts`). `index.html` không đổi. | Sonnet |
| 2026-07-04 | 1 | PASS | Convert `utils.ts`,`graph-utils.ts` nguyên vẹn. `constants.ts` chỉ giữ 10 hằng số hình học; tách nhóm biến runtime canvas (`state,nextId,...,renameMode`) sang JS mới `editor/canvas-state.js` (nợ kỹ thuật, gộp xử lý cùng Tier D sau) — tránh `TS2451` redeclare với `store.ts`. Thêm 1 thẻ `<script>` cho `canvas-state.js` trong `index.html`. `.gitignore`: ignore hẹp 3 file `core/{utils,constants,graph-utils}.js` (build artifact), không ignore cả `src/web/js/**` như Q4 gốc vì `canvas-state.js` là JS nguồn thật. | Sonnet |
| 2026-07-05 | 2 | PASS | Convert `change-manager.ts`, `export.ts`, `unit-config.ts` (Q1 vẫn sạch — không có diff chưa commit). `change-manager.ts` thêm `declare function render()` (Tier D global). `export.ts` dùng type `GrafcetStudioProject.*` cho import/export project JSON + HTML snapshot; `devCategories` không có trong type `Project` chuẩn (chưa migrate sang DTO C#) nên truy cập qua `as unknown as { devCategories?: ... }`. Đối chiếu output `tsc` với `.js` gốc: hành vi giống hệt, chỉ khác `unitId: null`→`undefined` (tương đương do luôn check falsy) và bỏ biến `idMap` chết (không dùng ở bản gốc). Untrack 3 file `.js` build-artifact khỏi git + thêm `.gitignore`. | Sonnet |
|      |       |           |                            |                 |
|      |       |           |                            |                 |
