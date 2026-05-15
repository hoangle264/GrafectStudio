# GRAFCET Studio v2 — Architecture

## Tổng quan

```
┌─────────────────────────────────────────────────┐
│                  WPF Window                     │
│           (container, không có UI logic)        │
│                                                 │
│  ┌──────────────────────┐  ┌─────────────────┐  │
│  │      WebView2        │  │    C# Core      │  │
│  │                      │  │                 │  │
│  │  Toàn bộ HTML UI     │  │  Code Generator │  │
│  │  - Canvas GRAFCET    │◄─┤  AI Client      │  │
│  │  - Drag & Drop       │─►│  File I/O       │  │
│  │  - Panels / Sidebar  │  │  Project Mgmt   │  │
│  │  - localStorage      │  │                 │  │
│  └──────────────────────┘  └────────┬────────┘  │
│                                     │           │
└─────────────────────────────────────┼───────────┘
                                      │
                              Anthropic API
                              (claude-sonnet)
```

---

## Phân chia trách nhiệm

### HTML / WebView2
- Toàn bộ UI: canvas, panels, toolbar, sidebar
- Drag & Drop, connect, zoom/pan diagram
- State management (localStorage)
- Hiển thị generated code, streaming AI response

### C#
- **Code Generation:** Keyence Mnemonic, TwinCAT ST (Strategy Pattern `ICodeGenerator`)
- **AI Client:** gọi Anthropic API, giữ API key secure, buffer + stream response
- **File I/O:** Save/Open project qua native Windows dialog
- **WPF Shell:** host WebView2, không có business logic

### C# Internal — Prism & DryIoc
```
ViewModel ──► IEventAggregator.Publish(event)
                    │
            IWebViewBridgeService (subscriber)
                    │
            webView.ExecuteScriptAsync(...)
```

---

## Bridge API

### JS → C#

| Event | Payload | Mô tả |
|---|---|---|
| `GENERATE_CODE` | `{ platform, steps, transitions, actions, variables }` | User bấm Generate |
| `AI_REQUEST` | `{ type, prompt, diagramContext }` | User dùng AI feature |
| `SAVE_FILE` | `{ projectJson }` | Native Save dialog |
| `OPEN_FILE` | _(không có data)_ | Native Open dialog |

> **Debounce** áp dụng **chỉ cho `AI_REQUEST`** — đặc biệt quan trọng khi `diagramContext` lớn (hệ thống nhiều servo + cylinder).

### C# → JS

| Function | Tham số | Mô tả |
|---|---|---|
| `receiveGeneratedCode(code)` | string | Kết quả code generation |
| `receiveAiChunk(chunk)` | string | Streaming AI response (đã buffer) |
| `loadProjectData(json)` | string | Sau khi Open file |
| `updateDiagramState(actions)` | array | AI auto-fix: patch từng node cục bộ |
| `receiveError(error)` | object | Thông báo lỗi từ C# về JS |

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

## AI Integration

### API Key
- Lưu trong `appsettings.json` hoặc Windows Credential Store
- Không bao giờ expose sang HTML/JS

### Use Cases

| Feature | Mô tả |
|---|---|
| **Generate GRAFCET** | User mô tả text → AI trả JSON steps/transitions → JS load canvas |
| **Review diagram** | C# serialize table data → AI phát hiện deadlock, missing transition → trả `updateDiagramState` actions patch cục bộ |
| **Explain code** | Sau generate Keyence Mnemonic → AI comment từng dòng |
| **Suggest actions** | User chọn Step → AI suggest action blocks phù hợp |

### Streaming Pipeline
```
Anthropic API stream
      │
      ▼
C# buffer (gom chunk, flush mỗi 100ms)
      │
      ▼
ExecuteScriptAsync("receiveAiChunk(...)")
      │
      ▼
HTML render realtime
```

### Prompt Strategy
- System prompt = few-shot Keyence Mnemonic examples (RAG-lite)
- C# build prompt = `systemPrompt + diagramContext + userRequest`

---

## Tech Stack

| Layer | Technology |
|---|---|
| Shell | WPF (.NET 10) |
| UI Engine | WebView2 (Microsoft Edge Chromium) |
| Frontend | HTML + CSS + JS (từ web version) |
| State | localStorage (trong WebView2 profile) |
| Code Gen | C# — Strategy Pattern (`ICodeGenerator`) |
| AI | Anthropic API (`claude-sonnet-4-20250514`) |
| DI | Prism + DryIoc |
| Messaging | Prism `IEventAggregator` |
| Bridge Service | `IWebViewBridgeService` (custom interface) |
| Docking | AvalonDock (nếu cần panels ngoài WebView2) |
