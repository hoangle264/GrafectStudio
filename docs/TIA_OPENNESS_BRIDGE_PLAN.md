# TIA Openness Bridge Plan (.NET 8 ↔ .NET Framework 4.8)

## 1. Mục tiêu

Tách phần import TIA Openness ra khỏi app chính để tránh xung đột runtime giữa app hiện đại `.NET 8` và Siemens TIA Openness thường yêu cầu `.NET Framework 4.8`.

Kiến trúc mục tiêu:

```text
[ GrafcetStudio App .NET 8 ]
        |
        | Sinh Siemens Openness XML
        v
[ File XML trên ổ cứng ]
        |
        | Gọi bridge.exe + truyền JSON request
        v
[ TIA Bridge .NET Framework 4.8 ]
        |
        | Siemens.Engineering.dll / TIA Openness API
        v
[ TIA Portal / PLC Project ]
```

Mục tiêu chính:

- App chính tiếp tục chạy `.NET 8`.
- Không load `Siemens.Engineering.dll` trong app `.NET 8`.
- Bridge `.NET Framework 4.8` là nơi duy nhất gọi Siemens Openness API.
- Giao tiếp giữa app và bridge qua file XML + JSON request/response.
- Phase đầu chỉ hỗ trợ TIA Portal V19 để giảm độ phức tạp.

---

## 2. Nguyên tắc thiết kế

### 2.1. Đơn giản trước

Không làm ngay trong phase đầu:

- Multi-version TIA.
- TIA Portal Add-In.
- Named pipe / IPC phức tạp.
- Background service.
- Auto installer.
- DI nhiều tầng.
- Auto-discovery tất cả version TIA.

Chỉ làm:

- App `.NET 8` sinh XML như hiện tại.
- App gọi `GrafcetStudio.TiaBridge.V19.exe`.
- Bridge import XML vào TIA Portal V19.
- Bridge trả kết quả bằng JSON qua stdout và exit code.

### 2.2. File XML là contract chính

App chính và bridge không gọi trực tiếp code Siemens chung nhau. Boundary giữa hai bên gồm:

- File XML vật lý.
- JSON request.
- JSON response.

Ví dụ request:

```json
{
  "tiaVersion": "V19",
  "projectPath": "D:\\TIA\\SampleProject.ap19",
  "deviceName": "PLC_1",
  "plcName": "PLC_1",
  "targetFolderPath": "Program blocks/Grafcet",
  "blockName": "Grafcet_Main",
  "xmlPath": "D:\\Export\\Grafcet_Main.xml",
  "overwriteMode": "Overwrite"
}
```

Ví dụ response:

```json
{
  "success": true,
  "status": "Success",
  "message": "Imported Grafcet_Main into Program blocks/Grafcet.",
  "details": null,
  "tiaVersion": "V19"
}
```

---

## 3. Cấu trúc project đề xuất

Hiện tại:

```text
src/
  GrafcetStudio.App/          # WPF .NET 8
  GrafcetStudio.App.Tests/
  SimaticML/                  # .NET 8
```

Thêm mới:

```text
src/
  GrafcetStudio.TiaBridge.Contracts/
  GrafcetStudio.TiaBridge.V19/
```

### 3.1. `GrafcetStudio.TiaBridge.Contracts`

Target framework:

```xml
<TargetFramework>netstandard2.0</TargetFramework>
```

Lý do:

- App `.NET 8` dùng được.
- Bridge `.NET Framework 4.8` dùng được.
- Chứa model request/response chung.
- Không chứa Siemens reference.

Nội dung đề xuất:

```text
TiaBridgeRequest.cs
TiaBridgeResponse.cs
TiaBridgeStatus.cs
TiaBridgeOverwriteMode.cs
```

Ví dụ model:

```csharp
public sealed class TiaBridgeRequest
{
    public string TiaVersion { get; set; } = "V19";
    public string? ProjectPath { get; set; }
    public string DeviceName { get; set; } = "";
    public string PlcName { get; set; } = "";
    public string TargetFolderPath { get; set; } = "";
    public string BlockName { get; set; } = "";
    public string XmlPath { get; set; } = "";
    public string OverwriteMode { get; set; } = "FailIfExists";
}
```

### 3.2. `GrafcetStudio.TiaBridge.V19`

Target framework:

```xml
<TargetFrameworkVersion>v4.8</TargetFrameworkVersion>
```

Loại project:

```text
Console App .NET Framework 4.8
```

Nhiệm vụ:

- Nhận request JSON.
- Validate input.
- Load/call Siemens Openness V19.
- Attach vào TIA Portal đang chạy hoặc open project nếu được cung cấp `projectPath`.
- Tìm device, PLC software và block folder.
- Import XML.
- Trả response JSON.

Thư viện Siemens tham chiếu từ:

```text
C:\Program Files\Siemens\Automation\Portal V19\PublicAPI\V19
```

Khuyến nghị:

- `Copy Local = false` cho Siemens assemblies nếu dùng từ PublicAPI.
- Bridge là nơi duy nhất có reference tới `Siemens.Engineering.dll`.

---

## 4. Thay đổi trong app `.NET 8`

### 4.1. Giữ lại generator XML hiện tại

Không sửa lớn các phần:

- `SimaticML`.
- `siemens-lad` generator.
- XML generation.
- Export file XML.

### 4.2. Thêm service gọi bridge

Thêm service mới:

```text
src/GrafcetStudio.App/Services/Siemens/BridgeSiemensTiaProjectService.cs
```

Nhiệm vụ:

1. Nhận `SiemensPushRequest`.
2. Đảm bảo XML đã được ghi ra file vật lý.
3. Tạo `TiaBridgeRequest`.
4. Serialize JSON vào temp file.
5. Gọi process bridge:

```powershell
GrafcetStudio.TiaBridge.V19.exe --request "C:\Temp\tia-request.json"
```

6. Đọc stdout JSON.
7. Map response về `SiemensPushResult`.

### 4.3. Không dùng reflection Siemens trong app chính

Phase đầu nên:

- Giữ `ReflectionSiemensTiaProjectService.cs` như legacy/experimental nếu cần.
- Không dùng làm default path cho import.
- Chuyển default flow sang XML export only hoặc bridge mode khi được bật.

Biến môi trường đề xuất:

```powershell
$env:GRAFCETSTUDIO_TIA_IMPORT_MODE = "bridge"
$env:GRAFCETSTUDIO_TIA_BRIDGE_PATH = "C:\Path\To\GrafcetStudio.TiaBridge.V19.exe"
```

---

## 5. Flow runtime

### 5.1. Manual XML export fallback

```text
User generate Siemens LAD XML
       |
       v
App ghi file XML
       |
       v
User import thủ công trong TIA Portal
```

Đây là fallback bắt buộc và nên luôn được giữ lại.

### 5.2. Bridge import

```text
User bấm Push to TIA
       |
       v
App generate XML temp/file
       |
       v
App tạo request JSON
       |
       v
App start TiaBridge.V19.exe
       |
       v
Bridge attach/open TIA
       |
       v
Bridge import XML
       |
       v
Bridge in response JSON ra stdout
       |
       v
App hiển thị kết quả
```

---

## 6. Contract chi tiết

### 6.1. Request fields

| Field | Bắt buộc | Ghi chú |
|---|---:|---|
| `tiaVersion` | Có | Phase đầu chỉ `V19` |
| `projectPath` | Không | Có thể bỏ nếu TIA đã mở project |
| `deviceName` | Có | Tên device trong TIA |
| `plcName` | Có | Tên PLC/software item |
| `targetFolderPath` | Có | Ví dụ `Program blocks/Grafcet` |
| `blockName` | Có | Tên block mong muốn |
| `xmlPath` | Có | File XML vật lý |
| `overwriteMode` | Có | `FailIfExists`, `Overwrite`, `Rename` |

### 6.2. Response fields

| Field | Ý nghĩa |
|---|---|
| `success` | true/false |
| `status` | Mã trạng thái |
| `message` | Thông báo ngắn |
| `details` | Exception/log bổ sung nếu có |
| `importedPath` | Optional |
| `tiaVersion` | Optional |
| `exitCode` | Optional |

Status đề xuất:

```text
Success
InvalidRequest
BridgeNotFound
TiaNotInstalled
TiaAccessDenied
ProjectNotFound
DeviceNotFound
PlcNotFound
TargetFolderNotFound
XmlNotFound
ImportFailed
Timeout
UnexpectedError
```

---

## 7. Các phase triển khai

## Phase 0 — Xác nhận XML hiện tại

Mục tiêu:

- Xác nhận XML export hiện tại chạy ổn.
- Xác nhận manual import TIA V19 bằng file XML.

Việc cần làm:

1. Generate XML từ app.
2. Lưu file XML ra disk.
3. Import thủ công vào TIA V19.
4. Compile PLC software.
5. Ghi lại lỗi schema/tag nếu có.

Điều kiện qua phase:

```text
XML generator valid enough for manual import.
```

Nếu Phase 0 chưa pass, chưa nên làm bridge.

## Phase 1 — Tạo contract chung

Thêm project:

```text
src/GrafcetStudio.TiaBridge.Contracts/
```

Target:

```text
netstandard2.0
```

Chứa:

```text
TiaBridgeRequest
TiaBridgeResponse
TiaBridgeStatus
TiaBridgeOverwriteMode
```

Không reference Siemens.

## Phase 2 — Tạo bridge console V19

Thêm project:

```text
src/GrafcetStudio.TiaBridge.V19/
```

Target:

```text
.NET Framework 4.8
```

CLI tối thiểu:

```powershell
GrafcetStudio.TiaBridge.V19.exe --request "C:\Temp\tia-request.json"
```

Bridge làm:

1. Đọc JSON request.
2. Validate field.
3. Kiểm tra file XML tồn tại.
4. Kết nối TIA.
5. Import XML.
6. Ghi JSON response ra stdout.
7. Set exit code.

Exit code đề xuất:

```text
0 = success
1 = invalid request
2 = TIA unavailable/access denied
3 = target not found
4 = import failed
9 = unexpected error
```

## Phase 3 — Port logic import sang bridge

Tham khảo logic hiện tại từ:

```text
src/GrafcetStudio.App/Services/Siemens/ReflectionSiemensTiaProjectService.cs
```

Nhưng trong bridge `.NET Framework 4.8`, ưu tiên dùng direct Siemens API thay vì reflection:

```csharp
using Siemens.Engineering;
```

Service đề xuất trong bridge:

```text
TiaV19ImportService.cs
```

Các method chính:

```text
AttachToRunningPortal()
OpenPortal()
ResolveProject()
FindDevice()
FindPlcSoftware()
ResolveBlockFolder()
ImportBlock()
```

## Phase 4 — App `.NET 8` gọi bridge

Thêm service:

```text
BridgeSiemensTiaProjectService.cs
```

Service này:

- Không reference Siemens.
- Chỉ gọi process.
- Có timeout rõ ràng, ví dụ 60–180 giây.
- Parse response JSON.
- Map response sang `SiemensPushResult`.

Nếu bridge không tồn tại:

```text
Return BridgeNotFound / TiaOpennessUnavailable.
```

## Phase 5 — UI/UX tối thiểu

Không cần UI lớn trong phase đầu.

Cần có message rõ:

- XML đã được sinh thành công.
- Bridge chưa cấu hình.
- Bridge không tìm thấy.
- TIA không chạy/chưa mở project.
- Không tìm thấy device/plc/folder.
- Import failed.

Có thể thêm sau:

- Nút `Open XML folder`.
- Nút `Copy XML path`.
- Hiển thị command bridge debug.

## Phase 6 — Test

### 6.1. Test không cần TIA

Trong app `.NET 8`:

- Serialize request.
- Bridge path missing.
- Bridge timeout.
- Parse response success/failure.
- Invalid stdout.

Trong bridge:

- Validate request.
- Missing XML.
- Invalid JSON.

### 6.2. Test cần TIA thật

Manual checklist:

1. TIA V19 installed.
2. User thuộc Siemens TIA Openness group.
3. TIA mở project sample.
4. Generate XML.
5. Bridge import XML.
6. Verify block xuất hiện.
7. Compile PLC software.

---

## 8. Cấu hình đề xuất

### 8.1. Development

```powershell
$env:GRAFCETSTUDIO_TIA_IMPORT_MODE = "bridge"
$env:GRAFCETSTUDIO_TIA_BRIDGE_PATH = "C:\Users\NITRO 5\source\vscode\GrafectStudio\src\GrafcetStudio.TiaBridge.V19\bin\Debug\GrafcetStudio.TiaBridge.V19.exe"
```

### 8.2. Production/demo

Đặt bridge cạnh app:

```text
GrafcetStudio.App.exe
GrafcetStudio.TiaBridge.V19.exe
GrafcetStudio.TiaBridge.Contracts.dll
```

Nếu cần Siemens DLL, ưu tiên load từ PublicAPI thay vì copy vào app chính.

---

## 9. Rủi ro và cách giảm rủi ro

### Rủi ro 1: XML sinh ra chưa import được

Giảm rủi ro:

- Bắt buộc Phase 0 manual import pass trước.
- Lưu sample XML.
- Lưu checklist compile.

### Rủi ro 2: TIA Openness permission

Giảm rủi ro:

- Message rõ user cần vào group Siemens TIA Openness.
- Docs hướng dẫn sign out/restart.
- Bridge trả status `TiaAccessDenied`.

### Rủi ro 3: Bridge path/deployment

Giảm rủi ro:

- Env var rõ.
- App fallback manual XML.
- Nếu bridge mất thì không crash.

### Rủi ro 4: Multi-version

Giảm rủi ro:

- Phase đầu chỉ V19.
- Hardcode/chốt V19.
- Sau khi V19 ổn mới thêm version khác.

### Rủi ro 5: stdout không parse được

Giảm rủi ro:

- Bridge chỉ output JSON result ra stdout.
- Log debug ghi ra file hoặc stderr.
- App có thể parse dòng JSON cuối cùng nếu cần.

---

## 10. Không làm trong phase đầu

Để tránh phức tạp, không làm:

- TIA Portal Add-In.
- Named pipe.
- Service chạy nền.
- Plugin architecture nhiều version.
- Auto-discovery nhiều TIA version.
- Import trực tiếp trong app `.NET 8`.
- Copy Siemens assemblies vào app chính.
- DI nhiều tầng.

---

## 11. Thứ tự implementation tối thiểu

Nếu bắt đầu code, thứ tự nên là:

1. Thêm `GrafcetStudio.TiaBridge.Contracts`.
2. Thêm `GrafcetStudio.TiaBridge.V19` console `.NET Framework 4.8`.
3. Bridge nhận JSON và trả fake success trước.
4. App `.NET 8` gọi bridge fake success.
5. Sau khi đường dây app ↔ bridge ổn, mới thêm Siemens import thật.
6. Test với TIA V19 thật.
7. Tắt hoặc đánh dấu legacy `ReflectionSiemensTiaProjectService`.

---

## 12. Kết luận

Phương án Bridge nên được làm theo hướng:

```text
.NET 8 app = editor + generator + UI
.NET Framework 4.8 bridge = Openness import only
XML/JSON = boundary giữa hai thế giới
```

Không nên bắt đầu bằng multi-version hoặc Add-In. Nên bắt đầu bằng bridge V19 tối giản vì đây là cách ít rủi ro nhất và phù hợp với tình trạng app hiện tại.
