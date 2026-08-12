# GrafcetStudio v2

Ứng dụng thiết kế sơ đồ **GRAFCET / SFC** (theo chuẩn **IEC 60848**) và sinh mã tự động cho PLC & Robot đa nền tảng, tích hợp trợ lý AI thông minh.

---

## 📌 Tổng Quan Dự Án

**GrafcetStudio v2** kết hợp sức mạnh của UI Web hiện đại (HTML5/TypeScript/SVG Canvas) và hiệu năng desktop của C# .NET 10 (WPF + WebView2).

### Các tính năng chính
- 🎨 **Thiết kế Sơ đồ GRAFCET/SFC**: Kéo thả Canvas tương tác cao, tạo Step, Transition, Branch (AND/OR), Action Qualifiers (N, S, R, P, L, D...).
- 🏭 **Sinh mã PLC & Robot đa nền tảng**:
  - **Keyence KV Mnemonic**: Sinh mã Mnemonic chuẩn cho PLC Keyence.
  - **TwinCAT ST**: Sinh mã Structured Text cho Beckhoff TwinCAT.
  - **Siemens TIA Portal LAD (SimaticML XML)**: Dựng trực tiếp DB, UDT và Block FC dạng Ladder Logic (SimaticML) hỗ trợ import/push thẳng vào TIA Portal V16-V19 qua Openness Bridge.
  - **ABB RAPID**: Sinh lập trình Robot RAPID cho ứng dụng Pick & Place, Palletizing.
- 🤖 **Trợ lý AI Tích hợp (Gemini & Anthropic Claude)**:
  - Tự động sinh sơ đồ GRAFCET từ mô tả ngôn ngữ tự nhiên.
  - Review sơ đồ: Phát hiện lỗi logic, deadlock, thiếu transition và tự động patch sửa lỗi (`updateDiagramState`).
  - Gợi ý hành động (Suggest actions) và giải thích mã nguồn (Explain generated code).
- 📊 **Quản lý Cấu hình & Biến (Variables & Structs)**:
  - Hỗ trợ import dữ liệu thiết bị, Cylinder, Unit Station từ file CSV.
  - Quản lý Bảng biến cục bộ (Local Variable Table) và Bảng biến toàn cục (Global Variable Table / Struct Data).

---

## 🏗️ Kiến Trúc Hệ Thống

Dự án được xây dựng theo mô hình **Hybrid Desktop-Web Architecture**:

```text
┌─────────────────────────────────────────────────────────┐
│                     WPF Window                          │
│               (Container host WebView2)                 │
│                                                         │
│  ┌──────────────────────────┐  ┌─────────────────────┐  │
│  │   WebView2 (Chromium)    │  │       C# Core       │  │
│  │                          │  │  - Code Generator   │  │
│  │  Toàn bộ UI (HTML/TS/JS) │  │  - SimaticML Engine │  │
│  │  - Canvas GRAFCET (SVG)  │◄─┤  - AI Client (Gemini│  │
│  │  - Drag & Drop / Editor  │─►│    / Anthropic)     │  │
│  │  - Variable Tables       │  │  - File I/O Native  │  │
│  └──────────────────────────┘  └──────────┬──────────┘  │
└───────────────────────────────────────────┼─────────────┘
                                            │
                                    AI API (Gemini/Claude)
```

1. **Frontend (`src/web/`)**:
   - Viết bằng **TypeScript** (`src/web/ts/`) biên dịch ra Javascript (`src/web/js/`).
   - Quản lý Canvas, rendering đồ thị SVG, thao tác chỉnh sửa biến, bảng biểu.
2. **Backend Shell (`src/GrafcetStudio.App/`)**:
   - C# .NET 10 WPF host Microsoft Edge WebView2.
   - Sử dụng **Prism + DryIoc** (Dependency Injection, Event Aggregator).
   - Đảm nhận nhiệm vụ Heavy Logic: Sinh mã PLC/Robot, AI Client, Thao tác File I/O native Windows.
3. **Bridge Communication (`IWebViewBridgeService`)**:
   - Giao tiếp 2 chiều giữa JS và C# thông qua `chrome.webview.postMessage` và `ExecuteScriptAsync`.

👉 *Xem chi tiết tại [ARCHITECTURE.md](file:///c:/Users/NITRO%205/source/vscode/GrafectStudio/ARCHITECTURE.md).*

---

## 📁 Cấu Trúc Thư Mục

```text
GrafectStudio/
├── src/
│   ├── GrafcetStudio.App/    # Ứng dụng C# WPF Shell, Code Generators, AI Client, Services
│   │   ├── Generators/       # Strategy Generators: Keyence, TwinCat, Siemens, ABB Robot
│   │   ├── Services/         # AI Service, Config, Bridge Service, TIA Openness
│   │   └── ViewModels/Views/ # Giao diện WPF & MainWindow WebView2 Host
│   ├── SimaticML/            # Thư viện C# dựng XML SimaticML chuẩn cho Siemens TIA Portal
│   └── web/                  # Mã nguồn Frontend UI
│       ├── ts/               # TypeScript source code (Core, Editor, Codegen, Types)
│       ├── js/               # Compiled Javascript artifacts
│       └── index.html        # Giao diện HTML chính chạy trong WebView2
├── config/                   # Các file cấu hình mẫu (plc-profiles, device-library, v.v.)
├── templates/                # Template Handlebars (.hbs) phục vụ sinh mã PLC
├── assets/                   # Tri thức RAG patterns & rules (ABB RAPID patterns)
├── docs/                     # Tài liệu kỹ thuật dự án (Siemens Openness, AI Plan, TS Architecture)
├── tests/                    # Unit Test suite và Baseline Stability tests
├── package.json              # Khai báo dependency TypeScript & scripts build frontend
├── GrafectStudio.sln         # Visual Studio Solution (.NET 10)
└── README.md                 # Tài liệu hướng dẫn chính
```

---

## 🛠️ Yêu Cầu Môi Trường & Hướng Dẫn Cài Đặt

### Yêu cầu hệ thống
- **Hệ điều hành**: Windows 10 / 11 (64-bit).
- **.NET SDK**: .NET 10.0 SDK trở lên.
- **Node.js**: Node.js 18+ và npm (dùng cho TypeScript compiler).
- **TIA Portal (Tùy chọn)**: Siemens TIA Portal V16 - V19 (nếu cần sử dụng tính năng Direct Push mã qua Openness Bridge).

### 1. Build Frontend TypeScript
Mở terminal tại thư mục gốc dự án:

```powershell
# Cài đặt dev dependencies (TypeScript compiler)
npm install

# Kiểm tra kiểu TypeScript
npm run typecheck

# Biên dịch TypeScript sang Javascript (src/web/js)
npm run build
```

### 2. Build và Chạy Ứng dụng Desktop (WPF)

Mở Visual Studio 2022 / VS Code hoặc dùng .NET CLI:

```powershell
# Restore và build C# Solution
dotnet build GrafectStudio.sln

# Chạy ứng dụng WPF
dotnet run --project src/GrafcetStudio.App/GrafcetStudio.App.csproj
```

---

## 📚 Tài Liệu Kỹ Thuật

| Tài liệu | Nội dung |
|---|---|
| 📖 [ARCHITECTURE.md](file:///c:/Users/NITRO%205/source/vscode/GrafectStudio/ARCHITECTURE.md) | Chi tiết kiến trúc phân tầng C#/JS, Bridge API Schema & AI Streaming Pipeline. |
| 📋 [TECH_DEBT.md](file:///c:/Users/NITRO%205/source/vscode/GrafectStudio/TECH_DEBT.md) | Danh sách nợ kỹ thuật (Technical Debt) cần cải thiện. |
| 🔄 [plan.md](file:///c:/Users/NITRO%205/source/vscode/GrafectStudio/plan.md) | Nhật ký & Kế hoạch chuyển đổi toàn bộ Frontend từ JavaScript sang TypeScript. |
| 📂 [docs/README.md](file:///c:/Users/NITRO%205/source/vscode/GrafectStudio/docs/README.md) | **Thư viện tài liệu kỹ thuật trong `docs/`**: TIA Portal Openness, Siemens LAD DSL, AI API integration. |
| 🌐 [src/web/instruction.md](file:///c:/Users/NITRO%205/source/vscode/GrafectStudio/src/web/instruction.md) | Hướng dẫn chi tiết cho lập trình viên phát triển mô-đun Frontend `src/web`. |

---

## ⚡ Giấy Phép & Đóng Góp

Dự án phát triển nội bộ bởi **GrafectStudio Team**. Mọi đóng góp xin tuân thủ quy trình kiểm thử `npm run typecheck` và `dotnet test` trước khi gửi Pull Request.
