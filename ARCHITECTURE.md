# GrafcetStudio — Kiến trúc

## Tổng quan
```text
WPF Window (.NET 8, WebView2)
 ├─ Web UI (src/web)                 HTML + CSS + TypeScript → JS (classic script)
 │    canvas SVG, bảng biến, tree, AI chat, modal sinh code
 └─ C# Core (src/GrafcetStudio.App)  Prism + DryIoc, IEventAggregator
      ├─ Generators (ICodeGenerator, tra theo Platform)
      ├─ TemplateManager (Handlebars.Net) + TemplateLoader
      ├─ AI (IAiCompletionService: Mock / Gemini)
      ├─ File I/O, ConfigService
      └─ Siemens TIA push (Bridge / Reflection / Unavailable)
src/SimaticML                         dựng XML Siemens (FC, FB, DB, UDT)
src/GrafcetStudio.TiaBridge.Shared    source + MSBuild dùng chung cho mọi bridge TIA
src/GrafcetStudio.TiaBridge.V19/.V20  exe .NET 4.8 mỗi version TIA, gọi TIA Openness
src/GrafcetStudio.TiaBridge.Contracts model dùng chung App ↔ Bridge
```

## Phân chia trách nhiệm
**Web (`src/web`)**: toàn bộ giao diện, soạn sơ đồ, lưu `localStorage`, dựng payload sinh code (`payload.ts`), tính địa chỉ step, làm sạch context cho AI, xem trước/Apply đề xuất AI.

**C# (`src/GrafcetStudio.App`)**: sinh code, render template, gọi AI, hộp thoại file native, đẩy XML vào TIA. Địa chỉ step do web tính, C# chỉ đọc (thiếu là báo lỗi).

## Nền tảng sinh code
| Platform | Generator | Đầu ra |
|---|---|---|
| `Keyence` | `MultiFileGenerator` (+ `KeyenceGenerator`, Error, System, Orchestrator, DeviceManager, MapIO) | `.mnm` |
| `siemens-lad` | `SiemensLadDslGenerator` | XML FC (template `.hbs` dạng text → SimaticML) |
| `siemens-db` | `SiemensDbUdtGenerator` | XML UDT/DB |
| `abb-rapid` | `AbbRapidGenerator` | `.mod`, teach list, prompt log (cần AI thật) |
| `runtime-plan` | `RuntimePlanGenerator` | JSON debug |

Template Handlebars nằm ở `templates/`, nạp bởi `TemplateLoader` theo tên chuẩn (unitAuto, unitOrigin, step-body, devices/*.hbs...). Đầu ra Keyence dạng `điều kiện -> INST đích` được `MnemonicEmitter` đổi thành mnemonic.

## Giao thức bridge
**Web → C#** (`chrome.webview.postMessage({ type, payload })`):

| type | Việc |
|---|---|
| `GENERATE_CODE` | sinh code theo payload |
| `AI_REQUEST` | yêu cầu AI (có thể stream) |
| `SAVE_FILE` / `OPEN_FILE` | lưu/mở project |
| `EXPORT_CODE` | xuất file hoặc ZIP |
| `BROWSE_CODEGEN_PATH` | chọn đường dẫn device library / template / output |
| `READ_TEMPLATE_FILE` | đọc template cho Template Editor |
| `PUSH_SIEMENS_LAD` | sinh XML và đẩy vào TIA |

**C# → Web** (`ExecuteScriptAsync`): `receiveGeneratedCode`, `receiveError`, `receiveAiChunk`, `receiveAiStreamEvent`, `loadProjectData`, `updateDiagramState`, `receiveCodegenPath`, `receiveSavedPaths`, `receiveTemplateFile`, `receiveSiemensTiaPushResult`.

Đổi tên callback hoặc `type` phải sửa cả hai phía.

## Luồng AI
```text
UI (chọn intent) → ContextBuilder → Sanitizer (whitelist, bỏ path/secret)
→ AI_REQUEST → AiContractGuard (làm sạch lần 2 ở host)
→ IAiCompletionService (Mock | Gemini, có stream)
→ parser + validator → xem trước (dry-run) → Apply
```
- AI chỉ đề xuất, không tự sửa project.
- Intent: create-variable, clone-variable, map-io, create-flow, create-structure (map-io chưa có Apply).
- API key chỉ ở host qua biến môi trường (`GRAFCETSTUDIO_AI_MODE`, `GEMINI_API_KEY`...). Lưu key qua UI bằng DPAPI chưa làm (TD-001).

## Đẩy vào TIA Portal
`SiemensTiaPushOrchestrator` sinh XML rồi gọi `ISiemensTiaProjectService`:
- `bridge` (mặc định): chạy `GrafcetStudio.TiaBridge.{V19|V20}.exe` theo version chọn trong modal (`SiemensTiaConfig.TiaVersion`, mặc định V19), giao tiếp bằng file JSON và stdout. Bridge từ chối request của version khác.
- Version TIA của push ghi đè `TIA_VERSION` trong template (`CodegenPayload.tiaVersion`). Cả `siemens-lad` và `siemens-db` set `SimaticMLAPI.TIA_VERSION` rõ ràng qua `SiemensTiaXmlVersion`.
- Mỗi bridge chỉ là một csproj đặt `<TiaVersion>` và import `TiaBridge.Shared.targets`. `Siemens.Engineering.dll` tham chiếu `Private=false`, nạp lúc chạy qua `AssemblyResolve`. Máy không có Openness của version đó thì build ra stub báo `TiaNotInstalled`.
- `reflection`: nạp `Siemens.Engineering.dll` bằng reflection (chỉ để debug, không theo version chọn trong modal).
- Biến môi trường: `GRAFCETSTUDIO_TIA_IMPORT_MODE`, `GRAFCETSTUDIO_TIA_BRIDGE_PATH[_V{n}]` (exe hoặc thư mục), `GRAFCETSTUDIO_TIA_OPENNESS_DIR[_V{n}]`.
- Luôn giữ đường import XML thủ công. Chưa xác nhận import và compile thật trên TIA (V19 lẫn V20). Setup V20: `docs/SIEMENS_TIA_V20_QUICK_SETUP.md`.

## Công nghệ
| Lớp | Công nghệ |
|---|---|
| Host | WPF, .NET 8, WebView2 |
| Giao diện | HTML, CSS, TypeScript (tsc, không bundler) |
| DI / sự kiện | Prism 9 + DryIoc |
| Template | Handlebars.Net |
| Parser biểu thức | Sprache |
| Test | xUnit |
| AI | Gemini (qua `GeminiAiCompletionService`) |

Xem thêm [docs/README.md](docs/README.md).
