# Nợ Kỹ Thuật (Technical Debt)

Danh sách các vấn đề đã được xác định nhưng chưa được xử lý.
Mỗi mục ghi rõ **vấn đề**, **rủi ro**, **phương án đề xuất**, và **điều kiện để ưu tiên**.

---

## [TD-001] Bảo mật API Key nhập từ UI

| Trường | Nội dung |
|---|---|
| **Ngày ghi nhận** | 2026-08-04 |
| **Mức độ** | 🟠 Trung bình — chỉ ảnh hưởng khi người dùng chọn lưu key |
| **Module liên quan** | `ConfigService.cs`, `App.xaml.cs`, `DynamicAiCompletionService` (chưa tạo) |

### Vấn đề

Khi implement tính năng cho phép người dùng nhập Gemini API Key trực tiếp trên UI (thay vì set biến môi trường OS), key có nguy cơ bị lưu dưới dạng **plaintext** vào `config.json`:

```
C:\Users\<user>\AppData\Roaming\GrafcetStudio\config.json
```

[`ConfigService.cs:L88`](file:///c:/Users/NITRO%205/source/vscode/GrafectStudio/src/GrafcetStudio.App/Services/ConfigService.cs#L88) dùng `JsonSerializer.Serialize()` — không có bước mã hóa nào.

### Rủi ro cụ thể

- Bất kỳ phần mềm nào chạy cùng user đều đọc được file JSON → lấy được key
- Nếu `AppData` được sync lên OneDrive / cloud backup → key lộ ra ngoài máy
- Người dùng vô tình chia sẻ thư mục config → key bị lộ

> **Lưu ý:** `debug.log` đã an toàn — `ScrubEndpoint()` trong
> [`GeminiAiCompletionService.cs:L287`](file:///c:/Users/NITRO%205/source/vscode/GrafectStudio/src/GrafcetStudio.App/Services/Ai/GeminiAiCompletionService.cs#L287)
> đã che `key=<redacted>` trước khi ghi log.

### Phương án đề xuất khi xử lý

Dùng **Windows DPAPI** (`System.Security.Cryptography.ProtectedData`) — chuẩn bảo mật cho desktop app Windows:

```csharp
// Lưu key (mã hóa bằng danh tính Windows user hiện tại):
var encrypted = ProtectedData.Protect(
    Encoding.UTF8.GetBytes(apiKey),
    null,
    DataProtectionScope.CurrentUser
);
File.WriteAllBytes(
    Path.Combine(appData, "GrafcetStudio", "ai_key.bin"),
    encrypted
);

// Đọc key (chỉ giải mã được bằng cùng user Windows):
var raw = File.ReadAllBytes(keyPath);
var apiKey = Encoding.UTF8.GetString(
    ProtectedData.Unprotect(raw, null, DataProtectionScope.CurrentUser)
);
```

File `ai_key.bin` sẽ là bytes ngẫu nhiên — không đọc được bằng text editor.

### Điều kiện để ưu tiên xử lý

- Khi triển khai tính năng "Lưu API Key qua UI" (không còn yêu cầu set env var thủ công)
- Khi chuẩn bị phát hành cho người dùng cuối (production release)

---

## [TD-002] `GenerateFallbackJson` không đọc Actions/Transitions thực từ Grafcet

| Trường | Nội dung |
|---|---|
| **Ngày ghi nhận** | 2026-08-04 |
| **Mức độ** | 🟡 Thấp — chỉ ảnh hưởng khi AI fail; cơ chế Fallback vẫn sinh code chạy được |
| **Module liên quan** | [`AbbRapidGenerator.cs:L101-L188`](file:///c:/Users/NITRO%205/source/vscode/GrafectStudio/src/GrafcetStudio.App/Generators/Robot/AbbRapidGenerator.cs#L101-L188) |

### Vấn đề

Khi AI fail hoặc không được cấu hình, `GenerateFallbackJson` rơi vào kịch bản **hardcode Pick & Place 4 bước** (`stepIdx == 1..4`) thay vì đọc `Actions` và `Transitions` thực từ sơ đồ Grafcet. Đồng thời tự thêm 4 biến vị trí mẫu (`pHome`, `pWait`, `pPickup`, `pPlace`) bất kể người dùng có khai báo chúng hay không.

### Điều kiện để ưu tiên xử lý

- Sau khi fix xong Lỗi #2 (DI) và Lỗi #3/#4 (Prompt) để AI hoạt động đúng
- Khi muốn Fallback cũng phản ánh đúng flow Grafcet thay vì kịch bản mẫu

---

*File này được duy trì thủ công. Khi một mục được xử lý xong, chuyển nó sang mục `## Đã Xử Lý` bên dưới và ghi ngày hoàn thành.*

## Đã Xử Lý

### ✅ [TD-003] ABB RAPID Prompt — Rule 3 thiếu logic phân biệt `WaitDI`/`WaitDO` vs `WaitUntil`

| Trường | Nội dung |
|---|---|
| **Ngày ghi nhận** | 2026-08-09 |
| **Ngày hoàn thành** | 2026-08-09 |
| **Mức độ** | ~~🔴 Cao~~ → ✅ Đã xử lý |
| **Module liên quan** | [`RobotSnippetPromptBuilder.cs`](file:///c:/Users/NITRO%205/source/vscode/GrafectStudio/src/GrafcetStudio.App/Generators/Robot/RobotSnippetPromptBuilder.cs) — Rule 1, Rule 3, Rule 6, Rule 7 |

**Đã sửa trong cùng phiên (2026-08-09):**
- **Rule 1**: Thêm exemption list RAPID built-in identifiers (`v500`, `fine`, `z10`, `tool0`, `syncident`) — miễn trừ khỏi ràng buộc Variable Table.
- **Rule 3**: Thay danh sách Wait examples bằng **bảng dispatch theo cột `Address`**: có DI/DO prefix → `WaitDI`/`WaitDO`; Address rỗng → `WaitUntil <var> = TRUE;`; số giây → `WaitTime`.
- **Rule 6**: Thêm **scope clause** — chỉ xét các nhánh trong block IF/TEST đang đóng, không xét cross-block.
- **Rule 7**: Thêm **counting rule** + ví dụ — chỉ mở `IF`/`TEST` mới tính +1 depth; `ELSIF`/`ELSE`/`CASE` cùng cấp không tính thêm.
