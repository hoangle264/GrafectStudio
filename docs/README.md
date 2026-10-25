# GrafcetStudio

Ứng dụng desktop vẽ sơ đồ **GRAFCET / SFC** (IEC 60848) và sinh code cho PLC và Robot, có trợ lý AI đề xuất.

## Tính năng chính
- Vẽ sơ đồ trên canvas SVG: Step, Transition, nhánh song song (AND-split/join), Macro Step, action qualifier (N, S, R, P, L, D...).
- Quản lý project: Unit, Diagram, Struct Data, biến toàn cục, IO Mapping, PLC Block (DB/UDT).
- Import CSV/Excel: Unit Station, Struct Data, Physical IO.
- Sinh code (chạy ở C#):
  - **Keyence**: file mnemonic `.mnm` (Units, Error, System, Orchestrator, Devices), dùng template Handlebars.
  - **Siemens LAD**: XML SimaticML cho FC, từ template `.hbs` dạng text.
  - **Siemens DB/UDT**: XML cho UDT, DB (shared flow struct và block).
  - **ABB RAPID**: AI sinh snippet, qua linter, ghép vào skeleton thành `.mod`. Bắt buộc có AI thật.
- Đẩy XML vào TIA Portal qua Openness bridge (V19). Luôn có đường import thủ công.
- Trợ lý AI (Gemini): đề xuất biến, struct, IO mapping, flow. Luôn xem trước rồi mới Apply.

## Kiến trúc
```text
WPF Window (WebView2)
 ├─ Web UI (HTML + TypeScript → JS, classic script)   src/web
 └─ C# Core (Prism + DryIoc)                          src/GrafcetStudio.App
      ├─ Generators, Template (Handlebars), AI, File I/O
      └─ SimaticML (XML Siemens) + TiaBridge.V19 (.NET 4.8)
```
- Hai bên giao tiếp bằng `chrome.webview.postMessage` (web → C#) và `ExecuteScriptAsync` (C# → web).
- Chi tiết xem [ARCHITECTURE.md](ARCHITECTURE.md).

## Cấu trúc thư mục
```text
src/
  GrafcetStudio.App/             WPF host, generators, AI, services
  GrafcetStudio.App.Tests/       xUnit tests
  GrafcetStudio.TiaBridge.V19/   bridge .NET 4.8 gọi TIA Openness
  GrafcetStudio.TiaBridge.Contracts/  model dùng chung với bridge
  SimaticML/                     thư viện dựng XML Siemens
  web/                           ts/ (nguồn), js/ (biên dịch), css/, index.html
templates/   template .hbs (Keyence, siemens-lad, devices)
assets/      RAG và skeleton cho ABB RAPID
docs/        tài liệu kỹ thuật
tests/       báo cáo kiểm thử
```

## Yêu cầu và cách chạy
- Windows 10/11, **.NET 8 SDK**, Node.js 18+.
- TIA Portal V16–V19 (tùy chọn, chỉ cần khi dùng Push to TIA).

```powershell
npm install
npm run typecheck
npm run build          # biên dịch ts -> src/web/js
dotnet build GrafectStudio.sln
dotnet test
dotnet run --project src/GrafcetStudio.App/GrafcetStudio.App.csproj
```

## Cấu hình AI (biến môi trường)
- `GRAFCETSTUDIO_AI_MODE=gemini`
- `GEMINI_API_KEY` (hoặc `GRAFCETSTUDIO_GEMINI_API_KEY`)
- `GRAFCETSTUDIO_GEMINI_MODEL` (tùy chọn)

Không có cấu hình này, app dùng mock AI; riêng ABB RAPID sẽ báo lỗi chứ không sinh code.

## Tài liệu
| Tài liệu | Nội dung |
|---|---|
| [ARCHITECTURE.md](ARCHITECTURE.md) | Kiến trúc, bridge, AI |
| [TECH_DEBT.md](TECH_DEBT.md) | Nợ kỹ thuật |
| [plan.md](plan.md) | Kế hoạch chuyển JS sang TypeScript |
| [docs/README.md](docs/README.md) | Thư viện tài liệu kỹ thuật |
| [src/web/instruction.md](src/web/instruction.md) | Hướng dẫn phát triển phần web |
