# GRAFCET Studio v2 — Work Plan

## Phase 1 — WPF Shell & WebView2 host
- [ ] Tạo WPF project, cài `Microsoft.Web.WebView2` NuGet
- [ ] Tạo `MainWindow` host WebView2 fullscreen
- [ ] Load HTML version hiện tại vào WebView2
- [ ] Verify localStorage hoạt động bình thường
- [ ] Verify kéo thả canvas hoạt động y hệt browser

## Phase 2 — Bridge cơ bản & Mock Data
- [ ] Implement `WebMessageReceived` handler trong C#
- [ ] Định nghĩa `IWebViewBridgeService`, wire vào DryIoc
- [ ] Thêm `chrome.webview.postMessage` vào HTML cho event Generate
- [ ] Implement Mock CodeGenerator và mock AI chunking để test bridge
- [ ] C# nhận payload → gọi Mock service → trả về qua `ExecuteScriptAsync`
- [ ] Test end-to-end: diagram → generate (mock) → hiển thị code
- [ ] Test `receiveError` path: mock throw exception → JS hiển thị error

## Phase 3 — Code Generator (C#)
- [ ] Định nghĩa `ICodeGenerator` interface
- [ ] Implement `KeyenceMnemonicGenerator`
- [ ] Implement `TwinCatStGenerator`
- [ ] Unit test generator độc lập với UI

## Phase 4 — Native File I/O
- [ ] Intercept Save: JS postMessage → C# `SaveFileDialog` → ghi file
- [ ] Intercept Open: JS postMessage → C# `OpenFileDialog` → đọc file → push vào JS
- [ ] Xử lý file `.grafcet` (JSON) và export `.txt` code

## Phase 5 — AI Integration
- [ ] Tạo `AnthropicClient` trong C# (HttpClient + streaming)
- [ ] Lưu API key an toàn
- [ ] Implement chunk buffer (flush mỗi 100ms) → `ExecuteScriptAsync`
- [ ] Implement `updateDiagramState` cho Review diagram auto-fix
- [ ] Implement từng AI feature theo thứ tự ưu tiên:
  - [ ] Review diagram
  - [ ] Explain code
  - [ ] Suggest actions
  - [ ] Generate GRAFCET từ mô tả

## Phase 6 — Polish
- [ ] App icon, window title, About dialog
- [ ] Error handling & user-friendly messages
- [ ] Installer / publish
