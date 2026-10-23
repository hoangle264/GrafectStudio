# Nợ Kỹ Thuật (Technical Debt)

Danh sách các vấn đề đã được xác định nhưng chưa được xử lý.
Mỗi mục ghi rõ **vấn đề**, **rủi ro**, **phương án đề xuất**, và **điều kiện để ưu tiên**.

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

## [TD-004] Migrate project cũ khi mở (`migrateFlowAddressConfigs` / `migrateFlowControlState`)

| Trường | Nội dung |
|---|---|
| **Ngày ghi nhận** | 2026-10-23 |
| **Mức độ** | 🟡 Thấp — chỉ ảnh hưởng project lưu bởi bản cũ |
| **Module liên quan** | [`store.ts`](src/web/ts/core/store.ts) — `migrateFlowAddressConfigs`, `migrateFlowControlState`, `ensureFlowAddressConfig` |

### Vấn đề

Khi mở project, store tự điền giá trị mặc định cho các trường còn thiếu (`addressMode`, `boolAddressMode`, `baseMr`, `activeWord`, `completeWord`, `controlState`, `category`). Đây là lớp tương thích ngược cho project tạo bởi bản cũ, ngầm sửa dữ liệu mà người dùng không biết.

### Phương án đề xuất khi xử lý

Gắn `schemaVersion` vào project; chỉ chạy migrate khi version cũ hơn, hoặc bỏ hẳn khi không còn project cũ và để validate báo lỗi.

### Điều kiện để ưu tiên xử lý

- Khi xác nhận không còn project cũ cần mở
- Khi thêm migrate mới (cần cơ chế version thay vì chạy mọi lần)

---

## [TD-005] Gencode Orchestrator mới là skeleton, chưa có template

| Trường | Nội dung |
|---|---|
| **Ngày ghi nhận** | 2026-10-23 |
| **Mức độ** | 🟠 Trung bình — project có flow `category = "orchestrator"` không sinh được code Keyence |
| **Module liên quan** | [`SystemControlGenerator.cs`](src/GrafcetStudio.App/Generators/SystemControlGenerator.cs) — `GenerateOrchestrator`, [`MultiFileGenerator.cs`](src/GrafcetStudio.App/Generators/MultiFileGenerator.cs), [`CodeGeneratorService.cs`](src/GrafcetStudio.App/Generators/CodeGeneratorService.cs) — `ResolveTemplateKey` |

### Vấn đề

`GenerateOrchestrator` chỉ xuất JSON (`phase = 1`, `skeleton = true`) gồm danh sách flow orchestrator và `OrchestratorConfig.Elements`. Chưa có logic sinh mnemonic và chưa có `Orchestrator.hbs` trong `templates/simple` hay `templates/packml`.

Trước đây `Orchestrator.mnm` âm thầm ra JSON thô. Sau khi bỏ fallback template, codegen báo `Template '<profile>.Orchestrator' is not loaded` và dừng khi project có flow orchestrator.

### Phương án đề xuất khi xử lý

Thiết kế gencode orchestrator thật (từ `OrchestratorConfig.Elements`), thêm `Orchestrator.hbs` vào các profile. Không dùng lại fallback JSON thô.

### Điều kiện để ưu tiên xử lý

- Khi cần dùng flow orchestrator trong project thật

---

*File này được duy trì thủ công. Khi một mục được xử lý xong, chuyển nó sang mục `## Đã Xử Lý` bên dưới và ghi ngày hoàn thành.*

## Đã Xử Lý

### ✅ [TD-001] Bảo mật API Key nhập từ UI

| Trường | Nội dung |
|---|---|
| **Ngày ghi nhận** | 2026-08-04 |
| **Ngày hoàn thành** | 2026-10-23 |
| **Mức độ** | ~~🟠 Trung bình~~ → ✅ Đã xử lý |
| **Module liên quan** | [`AiKeyStore.cs`](src/GrafcetStudio.App/Services/Ai/AiKeyStore.cs), [`DynamicAiCompletionService.cs`](src/GrafcetStudio.App/Services/Ai/DynamicAiCompletionService.cs), [`AiSettingsOrchestrator.cs`](src/GrafcetStudio.App/Services/AiSettingsOrchestrator.cs), `ai-chat-ui.ts` |

**Đã sửa:**
- Người dùng nhập/xóa Gemini API key và model ngay trong panel AI (nút ⚙), không cần set biến môi trường.
- Key mã hóa bằng Windows DPAPI (`DataProtectionScope.CurrentUser`), lưu ở `%AppData%/GrafcetStudio/ai_key.bin`; model lưu riêng ở `ai_settings.json`. Không ghi key vào `config.json`.
- WebView chỉ nhận trạng thái (đã cấu hình, nguồn, model), không bao giờ nhận lại key.
- Thứ tự ưu tiên: key lưu từ UI → `GEMINI_API_KEY` → `GRAFCETSTUDIO_GEMINI_API_KEY`. `GRAFCETSTUDIO_AI_MODE=mock` ép dùng mock.
- `DynamicAiCompletionService` chọn Gemini/mock theo từng request nên key mới có hiệu lực ngay, không cần khởi động lại.

**Đã sửa thêm:**
- Chọn model qua dropdown lấy từ Gemini API (`GeminiModelLister`, nút "Load models"); lần gọi này cũng kiểm tra key đúng/sai nên thay cho nút Test connection.

- Mọi request tới Gemini (sinh nội dung, streaming, danh sách model) gửi key qua header `x-goog-api-key`, không còn nằm trong URL.

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
