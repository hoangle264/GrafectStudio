# Kế hoạch mở rộng PLC: TIA Portal V20 → Mitsubishi GX Works3

## Tóm tắt
- Giai đoạn 1 (Siemens): hỗ trợ TIA Portal V20 song song với V19, vẫn dùng SimaticML XML.
- Giai đoạn 2 (Mitsubishi): đẩy chương trình vào GX Works3 qua Open Interface (GXW3OpenIF), dùng PLCopen XML.
- Làm xong và test thật giai đoạn 1 rồi mới sang giai đoạn 2. Giai đoạn 2 bắt đầu bằng một bước khảo sát khả thi (M0). Nếu M0 không đạt thì dừng.

## Status Legend
- `[ ]` Pending
- `[~]` In progress
- `[x]` Done
- `[!]` Blocked

## Rules
- Luôn giữ đường import XML thủ công. Bridge lỗi hoặc không có thì người dùng vẫn nhận được file.
- Không phá V19 khi thêm V20. Mỗi phase phải còn build và push được V19.
- Mỗi version TIA một bridge exe riêng, không gộp nhiều version vào một exe bằng reflection.
- Không đóng gói lại DLL của Siemens hay Mitsubishi (`Siemens.Engineering.dll`, `GXW3OpenIF*.dll`).
- Giao tiếp app ↔ bridge giữ nguyên kiểu hiện tại: request JSON qua file tạm, response JSON qua stdout, exit code, timeout.

## Dependency Order
```text
S0 Khảo sát V20
 ↓
S1 Sửa lỗi có sẵn ở bridge V19
 ↓
S2 Tách code bridge dùng chung
 ↓
S3 Bridge V20
 ↓
S4 App chọn version TIA
 ↓
S5 Test, tài liệu, đóng gói          ← xong giai đoạn Siemens
 ↓
M0 Khảo sát khả thi GX Works3 (go/no-go)
 ↓
M1 Contracts + process runner dùng chung
 ↓
M2 Mô hình địa chỉ Mitsubishi
 ↓
M3 Generator PLCopen XML
 ↓
M4 Bridge GX Works3
 ↓
M5 Tích hợp app + UI
 ↓
M6 Test, tài liệu, đóng gói
```

---

## Giai đoạn 1 — Siemens TIA Portal V20

### S0 — Khảo sát V20
- [!] Cài TIA Portal V20 + Openness. Ghi lại Update đang dùng (khuyến nghị Update 4 trở lên).
- [!] Ghi lại đường dẫn thực tế của `Siemens.Engineering.dll` (dự kiến `Portal V20\PublicAPI\V20\`) và các DLL phụ thuộc đi kèm, nếu có.
- [!] Trong V20, export ra XML: một FC/FB LAD, một global DB, một UDT.
- [!] So với XML GrafcetStudio sinh ra: namespace `Interface/vX`, `FlgNet/vX`, `StructuredText/vX` và thuộc tính `Engineering version`.
- [!] Import thử bằng tay XML V19 hiện tại vào V20 để xem có chạy không.

#### Notes
- **Blocked (2026-10-25):** máy dev chỉ có TIA V15/V19, chưa có V20. Cần máy có V20 để làm S0.
- V20 hiện dùng chung nhánh `>= 19` (FlgNet v5, Interface v5, StructuredText v4), chỉ khác `Engineering version="V20"`. Chưa xác nhận.
- V20 có thể tách Openness thành nhiều DLL. Resolver của bridge đã nạp mọi `Siemens.Engineering*.dll` trong thư mục Openness, nhưng tham chiếu lúc build vẫn chỉ `Siemens.Engineering.dll`.
- [SimaticMLAPI.cs](../src/SimaticML/API/SimaticMLAPI.cs) đang chọn namespace theo `TIA_VERSION` (>= 19 thì dùng FlgNet v5). Nếu V20 nâng namespace thì phải thêm nhánh `>= 20`.

### S1 — Sửa lỗi có sẵn ở bridge V19
- [x] Đăng ký `AppDomain.CurrentDomain.AssemblyResolve += ResolveEngineeringAssembly` ở đầu `Main` trong [Program.cs](../src/GrafcetStudio.TiaBridge.Shared/Program.cs). Hiện hàm này có nhưng chưa được gọi.
- [x] Bỏ việc copy `Siemens.Engineering.dll` cạnh exe khi build theo biến `GRAFCETSTUDIO_TIA_OPENNESS_DIR`, chuyển sang `Private=false` và nạp lúc chạy.
- [x] `SimaticMLAPI.TIA_VERSION` là static toàn cục. DB/UDT generator đang dùng giá trị LAD generator để lại (hoặc mặc định 17). Cả hai generator phải set version rõ ràng.
- [~] Test lại push V19 trên máy thật.

#### Notes
- Đã xác nhận: bridge V19 không có DLL cạnh exe vẫn nạp được Openness từ `Portal V19\PublicAPI\V19` và gọi `TiaPortal.GetProcesses()`. Lúc test TIA không chạy nên trả `TiaNotInstalled`.
- Còn lại: mở TIA V19 + project test, chạy push thật (import + kiểm tra block).

### S2 — Tách code bridge dùng chung
- [x] Đưa `Program.cs` và `ImportService` vào một thư mục dùng chung, ví dụ `src/GrafcetStudio.TiaBridge.Shared/`. Mỗi csproj version link source vào bằng `<Compile Include=... Link=...>`.
- [x] Bỏ các chuỗi "V19" cứng. Version lấy từ một property MSBuild (`TiaVersion`), sinh thành hằng số (ví dụ `DefineConstants` hoặc một file `BridgeVersion.g.cs`).
- [x] Đường dẫn DLL mặc định dựng từ version: `Portal V{n}\PublicAPI\V{n}\Siemens.Engineering.dll`.
- [x] Đổi tên `TiaV19ImportService` → `TiaImportService`.

#### Notes
- Không tạo được DLL dùng chung vì code tham chiếu `Siemens.Engineering` của một version cụ thể. Phải dùng shared source.
- Nếu V20 thay đổi API thì đặt phần khác biệt sau `#if TIA_V20`.
- Đã làm: `src/GrafcetStudio.TiaBridge.Shared/TiaBridge.Shared.targets` chứa toàn bộ cấu hình build. Version csproj chỉ đặt `<TiaVersion>` rồi import file này. Hằng số sinh ra trong `obj/BridgeVersion.g.cs`, define `TIA_V19`/`TIA_V20`.
- Thiếu Openness của version đó thì build ra stub (define `TIA_OPENNESS_MISSING`) luôn trả `TiaNotInstalled`, để build solution không vỡ trên máy chỉ có một version.
- Bridge từ chối request có `TiaVersion` khác version của nó.

### S3 — Bridge V20
- [x] Tạo `src/GrafcetStudio.TiaBridge.V20/GrafcetStudio.TiaBridge.V20.csproj` (net48), link shared source, `TiaVersion=V20`.
- [x] Thêm vào solution.
- [!] Build trên máy có V20, sửa lỗi API nếu có.
- [!] Chạy tay `GrafcetStudio.TiaBridge.V20.exe --request <file.json>` với một project V20 đang mở.

### S4 — App chọn version TIA
- [x] Thêm `TiaVersion` (mặc định `"V19"`) vào `SiemensTiaConfig` trong [ConfigService.cs](../src/GrafcetStudio.App/Services/ConfigService.cs) và vào `SaveSiemensTiaConfigAsync`.
- [x] Thêm ô chọn "TIA version" (V19 / V20) trong modal cấu hình push (`src/web/ts/codegen/modal.ts`, `payload.ts`) và trong payload `PUSH_SIEMENS_LAD`.
- [x] `BridgeSiemensTiaProjectService`: chọn exe theo version (`GrafcetStudio.TiaBridge.{V}.exe`), tìm theo thứ tự: biến môi trường → cạnh app → `tia-bridge/` → thư mục build trong repo. Gửi `TiaVersion` thật trong request.
- [x] Generator nhận version từ cấu hình push: `TIA_VERSION` trong template là mặc định, cấu hình push ghi đè (xem Quyết định mở #1).
- [x] Thông báo lỗi ghi đúng version (không còn "TIA Portal V19" cứng).

#### Notes
- Ô chọn TIA nằm ở dòng "Siemens TIA mode", áp dụng cho cả Preview XML và Push (`CodegenPayload.tiaVersion`).
- Biến môi trường bridge: `GRAFCETSTUDIO_TIA_BRIDGE_PATH_V{n}` (exe) thắng `GRAFCETSTUDIO_TIA_BRIDGE_PATH` (exe hoặc thư mục).

### S5 — Test, tài liệu, đóng gói
- [x] Unit test: chọn đường dẫn bridge theo version, request JSON chứa đúng `TiaVersion`, generator sinh đúng namespace theo version.
- [x] Cập nhật [SiemensTiaProjectServiceTests.cs](../src/GrafcetStudio.App.Tests/SiemensTiaProjectServiceTests.cs) (đang assert `"V19"` cứng).
- [ ] Test tay trên máy thật theo [SIEMENS_TIA_LAD_MANUAL_TEST_CHECKLIST.md](SIEMENS_TIA_LAD_MANUAL_TEST_CHECKLIST.md), chạy cho cả V19 và V20.
- [x] Viết `SIEMENS_TIA_V20_QUICK_SETUP.md`, cập nhật `ARCHITECTURE.md` (mục "Đẩy vào TIA Portal").
- [x] `publish-portable.ps1`: đóng gói cả hai bridge vào `tia-bridge/`.

### Ngoài phạm vi (ghi lại cho sau)
- Định dạng SIMATIC SD (`.s7dcl` + `.s7res` YAML) từ V20 Update 4: chỉ có từ V20, còn mới, có lỗi đã biết. Chỉ cân nhắc khi cần diff trên Git hoặc khi cần tính năng chỉ V20 có.
- Compile block sau khi import (`PlcSoftware.GetService<ICompilable>()`), làm sau nếu cần.

---

## Giai đoạn 2 — Mitsubishi GX Works3

### M0 — Khảo sát khả thi (go/no-go)
- [ ] Kiểm tra máy có GX Works3 có `GXW3OpenIF32.dll` / `GXW3OpenIF64.dll` không, ghi lại version GX Works3.
- [ ] Hỏi Mitsubishi / đại lý: Open Interface có SDK + tài liệu không, điều kiện bản quyền khi dùng trong phần mềm thương mại.
- [ ] Xác định dạng API của DLL (native C export, COM hay .NET) và kiến trúc x86/x64. Dùng `dumpbin /exports` hoặc nạp thử trong .NET.
- [ ] Viết một console thử: mở project `.gx3`, import một POU PLCopen XML, build, đọc lỗi.
- [ ] Trong GX Works3, export ra PLCopen XML: một Program ladder, một Program ST, một FB, global label, một SDT. Lưu làm mẫu vào `docs/schemas/mitsubishi/`.
- [ ] Kiểm tra GX Works3 có menu import PLCopen XML bằng tay không (để làm đường dự phòng).
- [ ] **Quyết định go/no-go.** No-go thì chỉ làm generator + xuất file để import tay (M2, M3, M5 rút gọn), bỏ M4.

#### Notes
- Không dùng UI Automation (giả lập click/dán vào GX Works3) cho sản phẩm vì quá dễ vỡ.

### M1 — Contracts + process runner dùng chung
- [ ] Đổi `GrafcetStudio.TiaBridge.Contracts` → `GrafcetStudio.PlcBridge.Contracts` (hoặc thêm project mới, giữ type TIA cũ để không vỡ).
- [ ] Tách phần chạy process của [BridgeSiemensTiaProjectService.cs](../src/GrafcetStudio.App/Services/Siemens/BridgeSiemensTiaProjectService.cs) (start, timeout, kill, đọc stdout/stderr, parse JSON) thành `BridgeProcessRunner` dùng chung.
- [ ] Siemens chuyển sang dùng runner chung. Test S5 vẫn phải pass.

### M2 — Mô hình địa chỉ Mitsubishi
- [ ] Thêm hãng Mitsubishi vào mô hình I/O của device template: X/Y/M/D/T/C. Lưu ý X/Y đánh số bát phân trên iQ-F, thập lục phân trên iQ-R. Cần xác nhận lại với dòng CPU đang dùng.
- [ ] Quy ước bit step GRAFCET (ví dụ dải `M`), timer, label cho FB instance.
- [ ] Phân biệt label (khuyến nghị) và device trực tiếp trong code sinh ra.

### M3 — Generator PLCopen XML
- [ ] Generator mới `mitsubishi-st`: sinh POU **ST** trước. ST trong PLCopen XML chỉ là text, nên dễ đúng hơn LD.
- [ ] Generator global label + SDT (tương đương `siemens-db`).
- [ ] Sau khi ST chạy được trên máy thật: generator `mitsubishi-ld` (LD trong PLCopen XML có toạ độ/kết nối đồ hoạ, khó hơn nhiều).
- [ ] Template `.hbs` cho thiết bị (cylinder, …) theo bản Mitsubishi.
- [ ] Đối chiếu file sinh ra với mẫu đã export ở M0.

### M4 — Bridge GX Works3
- [ ] `src/GrafcetStudio.GxWorks3Bridge/` (net48, đúng kiến trúc x86/x64 của DLL).
- [ ] Các thao tác: mở/attach project, import POU, import label/SDT, ghi đè/bỏ qua khi trùng, build, trả về lỗi build.
- [ ] Tìm DLL: biến môi trường `GRAFCETSTUDIO_GXW3_DIR` → thư mục cài mặc định `C:\Program Files (x86)\MELSOFT\GPPW3`.
- [ ] Mã lỗi/status tương đương TIA: không có GX Works3, không thấy project, import lỗi, build lỗi, timeout.

### M5 — Tích hợp app + UI
- [ ] `IMitsubishiProjectService` + bản Bridge và Unavailable, đăng ký trong `App.xaml.cs` giống `RegisterSiemensTiaServices`.
- [ ] `MitsubishiPushOrchestrator` + sự kiện `PUSH_MITSUBISHI`.
- [ ] Cấu hình push: đường dẫn project `.gx3`, tên program/FB, chế độ ghi đè.
- [ ] UI: chọn hãng PLC (Siemens / Mitsubishi) trong modal sinh code, chỉ hiện trường cấu hình của hãng đã chọn.

### M6 — Test, tài liệu, đóng gói
- [ ] Unit test generator (snapshot XML), test chọn bridge, test parse response.
- [ ] Checklist test tay `MITSUBISHI_GXW3_MANUAL_TEST_CHECKLIST.md`.
- [ ] `MITSUBISHI_GXW3_SETUP.md`: cài Open Interface, biến môi trường, giới hạn.
- [ ] `publish-portable.ps1`: thêm bridge GX Works3, **không** kèm DLL của Mitsubishi.

---

## Quyết định mở
1. ~~**Version TIA lấy từ đâu**~~ — Đã chốt: cấu hình push ghi đè `TIA_VERSION` trong template.
2. **Tự dò version TIA đang chạy:** có cần app thử lần lượt bridge V20 → V19 khi người dùng không chọn không? Mặc định là không, người dùng tự chọn.
3. **Mitsubishi ST hay LD trước:** khuyến nghị ST trước, LD sau.
4. **Dòng CPU Mitsubishi mục tiêu:** iQ-F (FX5) hay iQ-R. Ảnh hưởng tới cách đánh địa chỉ X/Y.

## Rủi ro
- V20 đổi namespace SimaticML hoặc API Openness. Xử lý ở S0, cách ly bằng `#if TIA_V20`.
- Open Interface không có tài liệu chính thức hoặc bản quyền không cho dùng thương mại. Đây là lý do có M0 go/no-go.
- DLL Mitsubishi là x86 hoặc native nên bridge phải build đúng kiến trúc.
- PLCopen XML của GX Works3 có phần riêng của hãng, không dùng lại nguyên cho CODESYS/ABB được.
- Chưa test trên máy thật: bridge V19 hiện cũng chưa được xác nhận import + compile thật (theo `ARCHITECTURE.md`).
