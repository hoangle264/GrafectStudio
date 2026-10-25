# GrafcetStudio Web — Hướng dẫn phát triển (`src/web`)

Nếu tài liệu này mâu thuẫn với code, tin code.

## 1. Vai trò phần web
- Vẽ sơ đồ GRAFCET trên canvas SVG, quản lý project (Unit, Diagram, Struct Data, biến, IO Mapping, PLC Block).
- Dựng payload gửi sang C# để sinh code. **Web không tự sinh code nữa**; `window.__ONLY_CSHARP_CODEGEN__ = true`.
- Dữ liệu lưu bằng `localStorage` (`gf2-project`, `gf2-diag-<id>`).

## 2. Cấu trúc
```text
src/web/
  index.html        thứ tự <script> là bắt buộc
  css/
  ts/               NGUỒN (core, editor, codegen, ai, types)
  js/               kết quả biên dịch + một số file JS thuần
```
- Biên dịch: `npm run build` (tsc, `module: none`, đầu ra `src/web/js`). Không sửa tay file js sinh từ ts.
- JS thuần còn lại (sửa trực tiếp): `canvas.js`, `events.js`, `elements.js`, `project.js`, `actions.js`, `context-menu.js`, `canvas-state.js`, `graph-utils.js` (một phần).
- Mô hình TS ↔ JS: logic dữ liệu nằm ở `ts/*.ts` đăng ký qua `GrafcetStudioInterop.registerBridge(...)` (`store`, `vars`, `ioMapping`, `tree`, `tables`, `excelImport`, `codegenPayload`, `ai`); các file `*-ui` lo DOM.

## 3. Thứ tự nạp script (theo `index.html`)
1. `core`: interop, utils, graph-utils, store-persistence
2. `editor/canvas-state`, `core/store`, `core/constants`
3. `ai/*`: contracts, sanitizer, context-builder, parser, mock-service, apply, bridge
4. `editor`: ai-chat-ui, change-manager, actions, panels, canvas, context-menu, elements, events
5. `editor`: tree, tree-ui, tree-devices-ui, tree-diagrams-ui, project, export
6. `editor`: tables, tables-ui, vars, io-mapping, vars-ui, io-mapping-ui, excel-import, excel-import-ui
7. `codegen`: unit-config, payload, template-editor, modal, modal-selectors, modal-host

Hàm gọi từ HTML (`onclick`...) phải còn trên `window`. Đổi thứ tự hoặc xóa global dễ gây lỗi âm thầm.

## 4. Mô hình dữ liệu
`project` gồm: `units`, `diagrams`, `devices` (Struct Data), `variables {imported, user}`, `excelVars`, `unitConfig` (cũ), `ioMapping`, `plcConfig`, `blocks`, `sharedFlowStruct`.

Ba lớp biến:
1. `state.vars`: biến cục bộ của diagram đang mở.
2. `project.variables` (`imported`, `user`): biến toàn cục, nguồn chính.
3. `project.excelVars` và `project.unitConfig`: dữ liệu import / cũ, vẫn được gom vào payload.

Quy tắc:
- `signalAddresses` khóa theo **tên signal** của Struct Data. Template phải dùng đúng tên, không tự đặt alias.
- Struct Data đổi tên signal thì `tree.ts` tự đổi khóa trong biến liên quan.
- Biến kiểu Struct Data có `signalAddresses`; kiểu nguyên thủy chỉ có `address`.
- Biến gán vào PLC Block có `declarationMode = SymbolicBlock` và `blockId`.

## 5. Địa chỉ step
Tính trong `payload.ts` (`resolveStepAddress`), C# chỉ đọc.
- `bool`: cặp `exec/done` liên tiếp từ `baseMr`, kiểu `linear` hoặc `block`.
- `word`: tối đa 32 step; mỗi step 1 bit trong word `activeWord`/`completeWord` (hoặc tag). Keyence: `DM0.3`; Siemens: `"Tag".%X3`.
- Kiểm tra trùng vùng MR giữa các diagram cùng unit (`validateUnitAddressConfig`).

## 6. Diagram và Macro
- `Macro`: chạy được. `MacroStep`: được gọi bởi step `kind = macro` (`macroFlowId`).
- Mỗi MacroStep chỉ được 1 step macro gọi, cùng unit, không lồng nhau.
- Biến `MacroPort` trùng tên MacroStep là tùy chọn; nếu có thì phải đúng format.

## 7. Sinh code (modal)
`showGenerateCodeModal()` có các target: `Keyence`, `siemens-lad`, `siemens-db`, `abb-rapid`.
1. Chọn Unit (hoặc "Send all").
2. `cgBuildCSharpPayload` → `GENERATE_CODE` (hoặc `PUSH_SIEMENS_LAD` nếu bật Push to TIA).
3. C# trả về `receiveGeneratedCode` → hiển thị nhiều file, copy/tải/ZIP.
4. Đường dẫn: Device Library, Template Root, Output Folder (lưu qua `ConfigService`).
5. Template Editor chỉ sửa và tải file `.hbs`; muốn có hiệu lực phải lưu vào Template Root.

`unit-config.ts` giờ chỉ là cầu nối tối thiểu (tìm Unit Station), không còn engine IL.

## 8. Import CSV
`showExcelImportModal()` hỗ trợ: **Unit Station**, **Struct Data**, **Physical IO**. (Cylinder CSV riêng không còn; dùng Struct Data với struct Cylinder.)
- Parser ở `ts/editor/excel-import.ts`, phần DOM ở `excel-import-ui.ts`.
- Địa chỉ phải khớp mẫu Keyence (MR, LR, DM, CR, AR, WR, HR).
- Struct Data: cột 0 = nhãn, các cột sau theo tên signal (có header) hoặc theo thứ tự signal.

## 9. AI
Chat nằm ở tab "AI Preview" của panel phải. Luồng: `ai-chat-ui` → context-builder → `AI_REQUEST` → C# → stream về `receiveAiStreamEvent` → parser/validator → xem trước → Apply (`ai/apply.ts`). Apply chỉ chạy khi proposal đã `validated`, và chặn Apply lặp.

## 10. Khi sửa
- Lỗi import/biến: xem `excel-import*.ts`, `vars*.ts`, `store.ts`.
- Lỗi payload/địa chỉ: `payload.ts`, `store.ts` (`findNextAvailableBaseMr`).
- Lỗi modal sinh code: `modal*.ts`, `template-editor.ts`.
- Lỗi canvas: `canvas.js`, `events.js`, `elements.js`, `graph-utils.ts`.
- Trước khi xong: `npm run typecheck` và `npm run build`.
- Đổi tên callback bridge phải sửa cả C# (`WebViewBridgeService`) và JS.
