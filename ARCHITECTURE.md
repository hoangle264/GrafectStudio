# GRAFCET Studio v2 — Architecture

## Tổng quan Kiến trúc

```text
┌─────────────────────────────────────────────────────────────────┐
│                           WPF Window                            │
│                  (container host WebView2)                      │
│                                                                 │
│  ┌─────────────────────────────┐  ┌──────────────────────────┐  │
│  │          WebView2           │  │         C# Core          │  │
│  │                             │  │                          │  │
│  │  Toàn bộ HTML/TS UI         │  │  Code Generators         │  │
│  │  - Canvas GRAFCET (SVG)     │  │  - Keyence Mnemonic      │  │
│  │  - Drag & Drop / Editor     │◄─┤  - TwinCAT ST            │  │
│  │  - Variable Tables          │─►│  - Siemens LAD ML XML    │  │
│  │  - localStorage Persistence │  │  - ABB RAPID Robot       │  │
│  │                             │  │  AI Client Services      │  │
│  │                             │  │  File I/O Native         │  │
│  └─────────────────────────────┘  └────────────┬─────────────┘  │
└────────────────────────────────────────────────┼────────────────┘
                                                 │
                                           AI Services API
                                       (Gemini & Claude Sonnet)
```

---

## Phân chia trách nhiệm

### 1. HTML / TypeScript / WebView2 (`src/web/`)
- Toàn bộ UI: canvas, panels, toolbar, sidebar, dialogs.
- Drag & Drop, kết nối node, zoom/pan sơ đồ GRAFCET/SFC.
- State management & persistence (localStorage).
- Hiển thị generated code, streaming AI response realtime.

### 2. C# Core (`src/GrafcetStudio.App/` & `src/SimaticML/`)
- **Code Generation (`ICodeGenerator` Strategy Pattern):**
  - Keyence Mnemonic (`KeyenceMnemonicGenerator`)
  - TwinCAT ST (`TwinCatStGenerator`)
  - Siemens TIA Portal LAD (`SiemensLadDslGenerator` + thư viện `SimaticML` dựng XML)
  - ABB RAPID Robot (`AbbRapidGenerator`)
- **AI Client Services:** Gọi Anthropic Claude / Google Gemini API, mã hóa & lưu trữ API key an toàn, buffer + stream response về UI.
- **File I/O:** Save/Open project native Windows dialog (`.grafcet` JSON), export code files (`.txt`, `.xml`, `.mod`).
- **WPF Shell:** Host WebView2 control, quản lý DI container (Prism + DryIoc).

### 3. C# Internal — Prism & DryIoc
```text
ViewModel ──► IEventAggregator.Publish(event)
                    │
            IWebViewBridgeService (subscriber)
                    │
            webView.ExecuteScriptAsync(...)
```

---

## Bridge API Protocol

### JS → C# (`chrome.webview.postMessage`)

| Event | Payload | Mô tả |
|---|---|---|
| `GENERATE_CODE` | `{ platform, steps, transitions, actions, variables }` | User bấm nút Generate code |
| `AI_REQUEST` | `{ type, prompt, diagramContext }` | User kích hoạt tính năng AI |
| `SAVE_FILE` | `{ projectJson }` | Mở Native SaveFileDialog và ghi file |
| `OPEN_FILE` | _(không có payload)_ | Mở Native OpenFileDialog và đọc file |

> **Debounce** áp dụng cho `AI_REQUEST` khi `diagramContext` lớn (hệ thống nhiều servo + cylinder).

### C# → JS (`ExecuteScriptAsync`)

| Function Callback | Tham số | Mô tả |
|---|---|---|
| `receiveGeneratedCode(code)` | string | Kết quả code generation từ C# |
| `receiveAiChunk(chunk)` | string | Streaming AI response (đã buffer 100ms) |
| `loadProjectData(json)` | string | Dữ liệu project đẩy vào UI sau khi Open file |
| `updateDiagramState(actions)` | array | AI auto-fix: patch từng node cục bộ |
| `receiveError(error)` | object | Thông báo lỗi từ C# về JS UI |

#### Schema `updateDiagramState`
```json
[
  { "op": "addStep",       "id": 7, "x": 100, "y": 200 },
  { "op": "addTransition", "from": 6, "to": 7, "condition": "X1" },
  { "op": "removeStep",    "id": 3 },
  { "op": "updateAction",  "stepId": 4, "actions": ["Y1 := TRUE"] }
]
```

#### Schema `receiveError`
```json
{ "source": "codeGen | aiClient | fileIO", "message": "..." }
```

---

## AI Integration Architecture

### API Key Management
- Lưu trữ trong mã hóa Windows DPAPI hoặc biến môi trường OS (`GRAFCETSTUDIO_GEMINI_KEY` / `ANTHROPIC_API_KEY`).
- Không bao giờ expose API Key sang môi trường HTML/JS.

### Streaming Pipeline
```text
Gemini / Anthropic API Stream
              │
              ▼
C# Buffer (gom chunk, flush mỗi 100ms)
              │
              ▼
ExecuteScriptAsync("receiveAiChunk(...)")
              │
              ▼
HTML UI render realtime
```

---

## Tech Stack Summary

| Layer | Technology |
|---|---|
| Shell Host | WPF (.NET 10) |
| UI Engine | WebView2 (Microsoft Edge Chromium) |
| Frontend | HTML5 + Vanilla CSS + TypeScript (`src/web/ts`) |
| State | localStorage (trong WebView2 profile) |
| Code Generators | C# — Strategy Pattern (`ICodeGenerator`) |
| SimaticML Engine | C# (.NET 10) — `SimaticML` library |
| AI Integration | Google Gemini API & Anthropic Claude API |
| Dependency Injection | Prism + DryIoc |
| Messaging | Prism `IEventAggregator` |
| Bridge Service | `IWebViewBridgeService` |

---

👉 *Xem danh mục tài liệu liên quan tại [docs/README.md](file:///c:/Users/NITRO%205/source/vscode/GrafectStudio/docs/README.md).*
