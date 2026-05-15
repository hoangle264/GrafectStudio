# GRAFCET Studio v2 — Copilot Instructions

## Kiến trúc tổng quan
- **WPF** chỉ là container host WebView2, không có UI logic
- **HTML/JS** chịu trách nhiệm toàn bộ UI, canvas, drag & drop, localStorage
- **C#** chỉ xử lý: Code Generation, AI Client, File I/O

## Rules bắt buộc

### UI
- Mọi logic UI nằm ở JS/HTML — không sinh code thao tác UI ở C#
- C# không giữ diagram state, không render bất kỳ thứ gì liên quan đến canvas

### Bridge (JS ↔ C#)
- JS → C#: dùng `chrome.webview.postMessage(payload)`
- C# → JS: dùng `webView.ExecuteScriptAsync("functionName(data)")`
- Bridge chỉ fire khi user **hoàn thành hành động** (mouseup, button click) — không fire trong quá trình drag
- Debounce chỉ áp dụng cho `AI_REQUEST`, không áp dụng cho `GENERATE_CODE`

### C# Internal
- Không gọi `ExecuteScriptAsync` trực tiếp từ ViewModel
- Dùng `IWebViewBridgeService` (inject qua DryIoc) để gọi JS
- Dùng `IEventAggregator` (Prism) để publish event nội bộ — decouple ViewModel khỏi WebView2

### AI / API
- API key không bao giờ được truyền sang JS
- Toàn bộ Anthropic API call thực hiện ở C#
- Buffer AI stream chunks (flush mỗi 100ms) trước khi gọi `ExecuteScriptAsync`

### State update từ AI
- Dùng operation list `updateDiagramState(actions[])` — không truyền lại toàn bộ JSON diagram
- Schema op: `addStep | addTransition | removeStep | updateAction`
