# Plan triển khai — Variable/Struct đa nền tảng PLC (Siemens TIA Portal DB/UDT)

> **Người thực thi:** Codex 5.5. Plan đủ chi tiết để chạy đúng, **không tự suy luận kiến trúc**.
> **Nhánh làm việc:** `feature/plc-variable-struct-redesign` — đã re-base lên **`origin/changeJavaScriptToTypeScript` (`c13c5ad`)**, KHÔNG phải `main`.
> **Vì sao:** `main` (`b08e352`) behind 45 commit. Toàn bộ hạ tầng Siemens (SimaticML DB/UDT, generator LAD, push TIA Openness) nằm trong 45 commit chưa merge đó. Plan bản trước sai vì dựa trên `main` cũ.
> **Mục tiêu lần này:** import variable vào Siemens (TIA Portal DB/UDT).
> **Trạng thái:** ✅ Đã chốt 5 câu hỏi thiết kế + đã explore toàn bộ module Siemens thật.

---

## ⭐ Bối cảnh hạ tầng ĐÃ TỒN TẠI (đọc kỹ trước khi code — đừng làm lại)

Task này **KHÔNG phải làm mới từ đầu**. Trên base hiện tại đã có sẵn:

### A. Thư viện `src/SimaticML/` (net8.0) — dựng block XML TIA thật
- `Blocks/BlockGlobalDB.cs` — **Global DB** (đúng thứ cần cho "import variable vào DB").
- `Blocks/BlockUDT.cs` — **UDT** (struct/type).
- `Blocks/BlockInstanceDB.cs` — **Instance DB** (khớp khái niệm "Instance" mục 4 thiết kế gốc).
- `Blocks/BlockDB.cs`, `BlockFB.cs`, `BlockFC.cs`.
- API thêm biến: `Section.AddMember(name, SimaticDataType)` → trả `Member`; **nested struct** qua `Member.AddMember(...)` (đệ quy sẵn). Kiểu: class `SimaticDataType` (static instances `BOOLEAN/INT/WORD/REAL/...`).
- Serialize: `SimaticMLAPI.CreateDocument(block)` → `XmlDocument` → `.Save(path)` / `.OuterXml`.
- ⚠️ **Cạm bẫy API đã xác minh:** `Section.AddVariable(...)` **ném exception** cho section `NONE` (UDT). Với UDT phải dùng **`AttributeList.NONE.AddMember(...)`**, không dùng `AddVariable`. DB dùng section `STATIC`.
- Phải gọi `block.Init()` sau khi `new`, rồi set `block.AttributeList.BlockName`.
- Ví dụ end-to-end có sẵn trong `src/SimaticML/SimaticMLExamples.cs` (`CreateGlobalDBExample`, `CreateUDTExample`) — **Codex đọc file này trước khi viết generator**.

### B. Generator `src/GrafcetStudio.App/Generators/Siemens/SiemensLadDslGenerator.cs`
- `Platform => "siemens-lad"`; implement `ICodeGenerator.GenerateFiles(CodegenPayload) : IEnumerable<CodegenFile>` (**multi-file**, KHÔNG phải `string Generate`).
- Dựng **`BlockFC` (logic ladder)** từ template Handlebars `.hbs` → DSL text → networks → LAD segments.
- ⚠️ **Điểm mấu chốt:** biến ở đây chỉ là `SimaticGlobalVariable(reference)` — **toán hạng trong ladder**, KHÔNG map vào DB/UDT (`CreateVariable` `:109-114`, có comment "Treat all refs as global operands unless later DECLARE support..."). ⇒ **Khoảng trống cho task này = sinh DB/UDT khai báo variable, thứ generator LAD chưa làm.**

### C. Interface generator + registry
- `src/GrafcetStudio.App/Generators/ICodeGenerator.cs`: `string Platform { get; }` + `IEnumerable<CodegenFile> GenerateFiles(CodegenPayload)`.
- `CodeGeneratorService` (`ICodeGeneratorService.Generate(platform, payload) : CodegenOutput`) map theo `Platform` (case-insensitive dict).
- DI đăng ký ở `App.xaml.cs:53-67`. Siemens LAD ở `:58` (`SiemensLadDslGenerator`). Siemens mnemonic (IL) ở `:57` (`ProfiledMnemonicGenerator(ProfileRegistry.Siemens.Id)`, Platform `"siemens"`).

### D. Push pipeline vào TIA Openness — `src/GrafcetStudio.App/Services/Siemens/`
- `ISiemensTiaProjectService.PushBlockXmlAsync(SiemensPushRequest)` — **generic**, nhận `XmlContent`/`XmlPath` + `BlockName` (không phân biệt DB/FC/UDT ở interface).
- 3 impl chọn theo env `GRAFCETSTUDIO_TIA_IMPORT_MODE` (`App.xaml.cs:155`): `bridge` (mặc định — chạy process net48 `GrafcetStudio.TiaBridge.V19`), `reflection` (in-process reflection), còn lại `Unavailable` (no-op).
- `SiemensTiaPushOrchestrator` nhận web command → sinh XML → push. ⚠️ **Hard-code `platform=="siemens-lad"`** ở `SiemensTiaPushOrchestrator.cs:135` (ném nếu khác).
- ⚠️ **Rủi ro UDT import đã xác minh:** bridge V19 import vào `PlcBlockGroup.Blocks` (`TiaV19ImportService`). Trong TIA Openness, **UDT nằm ở `PlcTypeGroup.Types`**, khác collection. ⇒ Push **DB** khả năng chạy; push **UDT** có thể KHÔNG được bridge hiện tại xử lý → xem **câu hỏi Q6** cuối plan.

### E. Web trigger (đã có)
- `src/web/ts/codegen/modal-host.ts:126` post message `PUSH_SIEMENS_LAD` với `{ platform:'siemens-lad', codegenPayload, ...tiaConfig }` (tiaConfig = projectPath/deviceName/plcName/targetFolderPath/overwriteMode).
- Handler C#: `MainWindow.xaml.cs:185` `case "PUSH_SIEMENS_LAD"` → orchestrator.

### F. Model core CHƯA đổi (nên phần thêm field của plan vẫn còn giá trị)
- `SignalVarType` vẫn `{Input, Output, Var}`.
- `DeviceVariable`/`DeviceType`/`DeviceSignal` chưa có `DeclarationMode`/`blockId`/`Instance`/nested.
- "Struct" hiện tại = `ProjectVariable.format` khớp `DeviceType.name` + `signalAddresses` **phẳng 1 cấp** (`vars.ts:117` `gvtGetSigList`, `store.ts:332` `normalizeVariableRecord`).
- "Macro"/"MacroStep" (`DiagramType 'Macro'/'MacroStep'`, `MacroBindingContext.cs`) là **lời gọi sub-Grafcet**, **KHÔNG liên quan struct dữ liệu**. Đừng nhầm. Chỉ dính nhau qua quy ước `Format=="MacroPort"`.

---

## Ràng buộc chung (áp dụng cho MỌI bước)

1. **Chỉ thêm field mới optional.** Không sửa kiểu / không đổi default / không xoá field cũ. Ngoại lệ đã được chốt: Bước 5 (sửa hàm expand) và Bước 6 (nới orchestrator + tạo generator) — đều có hướng dẫn riêng.
2. **Đồng bộ 2 lớp model:** C# `src/GrafcetStudio.App/Domain/...` (kèm `[JsonPropertyName]` camelCase) ↔ TS `src/web/ts/types/project.ts` (namespace `GrafcetStudioProject`).
3. **Khác biệt hãng đi qua registry/generator** (`Platform` id + `CodeGeneratorService`). **Không hardcode `if vendor=="siemens"`** trong model/store/parse dùng chung.
4. **Tận dụng hạ tầng có sẵn (mục ⭐).** Không tự viết lại SimaticML / push pipeline / trigger web. Chỉ **thêm generator mới + field model + parser**.
5. **3 bẫy build đã xác minh:**
   - **B1 — `tsc` emit ghi đè `.js` đã commit.** Sau khi sửa `.ts` chỉ chạy `npm run typecheck` (`tsc --noEmit`). KHÔNG `npm run build` trừ khi cố ý commit `.js` regenerate.
   - **B2 — `types/project.ts` là types-only**, `project.js` emit ra **không được `index.html` nạp** ⇒ không đặt giá trị runtime (const/enum-có-giá-trị) vào `project.ts`.
   - **B3 — `core/constants.js` là JS viết tay chưa migrate** ⇒ KHÔNG tạo `src/web/ts/core/constants.ts` (tsc emit sẽ đè). Hằng số TS mới đặt trong file `.ts` đã tồn tại & đã nạp (vd `core/store.ts`).
6. **Mỗi bước 1 commit riêng**, build/typecheck pass mới sang bước kế. Bước 1–5 độc lập; Bước 6 dùng field của 1–5.

### Lệnh verify chuẩn
- C#: `dotnet build src/GrafcetStudio.App/GrafcetStudio.App.csproj` (target `net10.0-windows`; SimaticML là `net8.0`, project-ref sẵn ở csproj:11).
- TS: `npm run typecheck`.
- Serialization: `JsonStringEnumConverter` bật toàn cục (`CodeGenerationOrchestrator.cs`) ⇒ enum C# ra string tên member, khớp union TS.

---

## Quyết định đã chốt

| # | Chủ đề | Quyết định |
|---|---|---|
| **Q1** | `Instance` metadata (Bước 1) | **Marker thuần** — thêm 1 member enum, không field FB type. (Lưu ý: SimaticML đã có `BlockInstanceDB` để dùng sau nếu cần.) |
| **Q2** | Root model C# (Bước 3) | **Không có `class Project` C#.** Trung tâm là `CodegenPayload`. `PlcBlock` khai TS trước; DTO C# chỉ tạo ở Bước 6 khi thêm `CodegenPayload.Blocks`. |
| **Q3** | Đệ quy struct lồng (Bước 5) | **Sửa trực tiếp hàm expand đang chạy** (`gvtGetSigList`). Bắt buộc verify struct 1 cấp output y hệt trước/sau. |
| **Q4** | Phạm vi Bước 6 | **Mở rộng** — tạo generator khai báo DB/UDT mới. **Cập nhật theo explore:** tận dụng `SimaticML.BlockGlobalDB/BlockUDT` + push pipeline có sẵn, KHÔNG viết XML tay. |
| **Q5** | `DeclarationMode` kiểu dữ liệu | **Giữ string** cả 2 phía. |

---

## Mục lục các bước

- [x] **Bước 1** — Thêm `Instance` vào `SignalVarType` (C# + TS)
- [x] **Bước 2** — Field `DeclarationMode` (AddressMapped / SymbolicBlock)
- [x] **Bước 3** — Model entity `PlcBlock` (DB / UDT / GVL) — TS trước
- [x] **Bước 4** — Field liên kết biến ↔ Block (`blockId`, default `""`)
- [x] **Bước 5** — Struct lồng struct (tối đa 3 cấp, `MaxNestingDepth` hằng số)
- [ ] **Bước 6** — Generator khai báo DB/UDT (SimaticML) + payload + parser import + push

---

## Bước 1 — Thêm `Instance` vào `SignalVarType`

**Q1 đã chốt: marker thuần.**

### File cần sửa
1. `src/GrafcetStudio.App/Domain/Enums/SignalVarType.cs:4-9` — thêm member `Instance` **cuối enum**.
2. `src/web/ts/types/project.ts:13` — thêm `'Instance'` vào union `SignalVarType`.

### Nội dung sửa cụ thể
- C# (giữ thứ tự Input=0/Output=1/Var=2, thêm cuối để không đổi giá trị ngầm định):
  ```csharp
  public enum SignalVarType { Input, Output, Var, Instance }
  ```
- TS `:13`:
  ```ts
  export type SignalVarType = 'Input' | 'Output' | 'Var' | 'Instance' | string;
  ```

### Ràng buộc bất biến
- Chỉ thêm member cuối. Không đánh số lại.
- **KHÔNG** sửa `io-mapping.ts:48` (lọc I/O direction) — biến `Instance` rơi vào `dir=''` và bị bỏ qua khỏi I/O mapping. Đây là hành vi ĐÚNG (instance không phải I/O vật lý). Ghi rõ trong commit.
- ⚠️ **Đồng bộ AI allow-list:** `src/web/ts/ai/apply.ts:257` và `ai/contracts.ts:248` hard-code `allowedStructureVarTypes = ['Input','Output','Var']`. **Chưa thêm `'Instance'` vào 2 chỗ này ở Bước 1** (nếu thêm sẽ cho phép AI sinh Instance — vượt phạm vi marker). Để nguyên; nếu sau này cần AI hỗ trợ Instance thì mở riêng. Ghi chú trong commit là "cố ý không đụng AI allow-list".

### Đồng bộ bắt buộc
- C# enum ↔ TS union. Không cần `[JsonPropertyName]` cho member enum.

### Tiêu chí done
- `dotnet build` + `npm run typecheck` pass.
- `grep -rn "Instance" src/GrafcetStudio.App/Domain/Enums/SignalVarType.cs src/web/ts/types/project.ts` thấy cả 2.
- Luồng cũ không đổi (io-mapping, UnitConfigGenerator `.ToString()`).

---

## Bước 2 — Field `DeclarationMode` (AddressMapped / SymbolicBlock)

**Q5 đã chốt: string.**

### File cần sửa
1. `src/GrafcetStudio.App/Domain/Models/DeviceVariable.cs` — thêm property optional (sau `SignalAddresses`, ~dòng 19).
2. `src/web/ts/types/project.ts` — thêm `type DeclarationMode` (cạnh `:16`) + field vào `interface DeviceVariable` (`:163`).

### Nội dung sửa cụ thể
- TS:
  ```ts
  export type DeclarationMode = 'AddressMapped' | 'SymbolicBlock' | string;
  ```
  Thêm vào `interface DeviceVariable`:
  ```ts
  declarationMode?: DeclarationMode;   // absent => AddressMapped (hành vi cũ)
  ```
- C# `DeviceVariable.cs`:
  ```csharp
  [JsonPropertyName("declarationMode")]
  public string? DeclarationMode { get; init; }   // null/absent => AddressMapped
  ```

### Ràng buộc bất biến
- Optional/nullable. Thiếu ⇒ mặc định `AddressMapped` (hành vi cũ). Không migration dữ liệu cũ, không set default khác null phía C#.
- **Chỉ khai field.** Không thêm nhánh xử lý theo mode ở bước này (rẽ nhánh nằm ở Bước 6 qua generator).

### Đồng bộ bắt buộc
- `[JsonPropertyName("declarationMode")]` ↔ TS `declarationMode`. String cả 2 phía.

### Tiêu chí done
- Build + typecheck pass.
- Load project cũ (JSON không có `declarationMode`) → ra null/undefined, luồng address cũ chạy nguyên.

---

## Bước 3 — Model entity `PlcBlock` (DB / UDT / GVL) — TS trước

**Q2 đã chốt: TS trước, không tạo class C# ở bước này** (DTO C# để Bước 6). `PlcBlock` là entity riêng có ID, chứa biến con **bằng tham chiếu ID**.

### File cần sửa
1. TS `src/web/ts/types/project.ts` — thêm `type BlockKind` + `interface PlcBlock` (cạnh các type khác, sau `:16`), và `blocks?: PlcBlock[]` vào `interface Project` (`:17` block).
2. **KHÔNG** đụng file C# ở bước này.

### Nội dung sửa cụ thể
```ts
export type BlockKind = 'DB' | 'UDT' | 'GVL' | string;
export interface PlcBlock {
  id: string;
  name: string;              // vd "DB_Motor", "UDT_Cylinder", "GVL_IO"
  kind: BlockKind;
  memberVarIds?: string[];   // tham chiếu ProjectVariable.id thuộc block
  comment?: string;
  [key: string]: unknown;
}
```
Thêm `blocks?: PlcBlock[];` vào `interface Project`.

### Ràng buộc bất biến
- Entity mới hoàn toàn. `blocks` optional ⇒ project cũ hợp lệ.
- Quan hệ biến→block 1 chiều bằng ID (chiều ngược ở Bước 4). Không nhúng object biến vào block.
- Không tạo aggregate `Project` C# (Q2). Class C# cho block xuất hiện ở Bước 6 dưới dạng DTO phẳng cho `CodegenPayload`.

### Đồng bộ bắt buộc
- Bước này thuần TS. Ghi chú trong commit: Bước 6 phải tạo DTO C# `PlcBlock` khớp camelCase (`id/name/kind/memberVarIds/comment`).

### Tiêu chí done
- `npm run typecheck` pass. Project cũ `project.blocks` ra undefined.

---

## Bước 4 — Field liên kết biến ↔ Block (`blockId`, default `""`)

> Biến `SymbolicBlock` thuộc 1 block cụ thể. Default `""` (KHÔNG `null`). Render: có block → `"DB_Motor".Ten`; không block (`""`) → `Ten`.

### File cần sửa
1. C# `src/GrafcetStudio.App/Domain/Models/DeviceVariable.cs` — thêm property.
2. TS `src/web/ts/types/project.ts` — thêm field vào `interface DeviceVariable` (`:163`).

### Nội dung sửa cụ thể
- TS: `blockId?: string;   // id PlcBlock; '' = không thuộc block`
- C#:
  ```csharp
  [JsonPropertyName("blockId")]
  public string BlockId { get; init; } = string.Empty;   // '' = không thuộc block; KHÔNG null
  ```

### Ràng buộc bất biến
- Default `""` (khác Bước 2 nullable) — theo thiết kế gốc chốt tránh `null` để check an toàn.
- Không auto-gán biến vào block (thiết kế: người dùng gán tay/import). Chỉ thêm field lưu trữ.
- Không viết code đồng bộ 2 chiều `PlcBlock.memberVarIds` ↔ `blockId` tự động ở bước này.

### Đồng bộ bắt buộc
- `[JsonPropertyName("blockId")]` ↔ TS `blockId`.

### Tiêu chí done
- Build + typecheck pass. Project cũ `blockId` ra `""`/undefined, không phá resolve address cũ.

---

## Bước 5 — Struct lồng struct (tối đa 3 cấp, `MaxNestingDepth` hằng số)

> **Hiện trạng:** struct 1 cấp — `gvtGetSigList` (`vars.ts:117-122`) trả phẳng `devType.signals`. `DeviceSignal.dataType` là **string** (`'Bool'/'Int'/...`), chưa trỏ DeviceType khác.
> **Mở rộng (additive):** cho phép `DeviceSignal.dataType` mang **tên 1 DeviceType khác** → tạo lồng. `MaxNestingDepth = 3`.
> **Q3 đã chốt: sửa trực tiếp hàm expand.**

### File cần sửa
1. **Hằng số `MaxNestingDepth`:**
   - C#: tạo `src/GrafcetStudio.App/Domain/Models/StructLimits.cs`: `public static class StructLimits { public const int MaxNestingDepth = 3; }`. KHÔNG rải số 3.
   - TS: **KHÔNG tạo `constants.ts`** (bẫy B3). Đặt trong file đã nạp — đề xuất `src/web/ts/core/store.ts`: `export const MAX_NESTING_DEPTH = 3;` (namespace store đã nạp qua `index.html:612`).
2. TS `project.ts` `DeviceSignal` (`:151`) — thêm optional (giữ `dataType: string` không đổi):
   ```ts
   nestedTypeId?: string;   // signal trỏ tới DeviceType lồng; optional, suy được từ dataType
   ```
3. C# `DeviceSignal.cs:6` — `[JsonPropertyName("nestedTypeId")] public string? NestedTypeId { get; init; }`
4. **Sửa `gvtGetSigList` (`vars.ts:117-122`)** — expand đệ quy có chặn depth.

### Nội dung sửa cụ thể
- Đệ quy: mỗi signal, nếu có `nestedTypeId` (hoặc `dataType` khớp `name` 1 DeviceType trong `context.project.devices`) → expand tiếp signals type con, **cộng path** (`parent.child.leaf`), **dừng khi depth == MAX_NESTING_DEPTH** (vượt → không expand thêm + warning, không throw). Signal primitive → trả nguyên như cũ.
- **Giữ chữ ký** `gvtGetSigList(context, variable): DeviceSignal[]` (interface `:188`) để không phá caller (`io-mapping.ts`, tables/tree). Đệ quy trong helper private nhận thêm `depth`.

### Ràng buộc bất biến
- **1 cấp cũ chạy y nguyên:** signal primitive (không khớp DeviceType, không `nestedTypeId`) → nhánh cũ, output **byte-for-byte y hệt**. Nhánh `variable.format === 'Cylinder'` (`vars.ts:120`) + fallback `CYL_SIGNALS` **giữ nguyên tuyệt đối**.
- `DeviceSignal.dataType` giữ kiểu string.
- Q3 cho phép sửa hàm đang chạy — Codex KHÔNG cần hỏi lại. Đổi lại: **verify before/after bắt buộc**.

### Đồng bộ bắt buộc
- `MaxNestingDepth` (C#) ↔ `MAX_NESTING_DEPTH` (TS) = 3, mỗi bên 1 nơi.
- `[JsonPropertyName("nestedTypeId")]` ↔ TS `nestedTypeId`.

### Tiêu chí done
- Build + typecheck pass.
- **Verify bắt buộc (Q3):** chọn ≥1 struct var 1 cấp có thật (vd `Cylinder`), gọi `gvtGetSigList` trước/sau khi sửa, so sánh list (name/path/dataType/varType) — **giống hệt**. Ghi cách so sánh trong commit/PR.
- Struct 2–3 cấp: expand đúng path lồng; cấp 4 chặn + warning.

---

## Bước 6 — Generator khai báo DB/UDT (SimaticML) + payload + parser + push

> **Mục tiêu chính.** Sinh biến `SymbolicBlock` gán `blockId` → generate **DB/UDT XML bằng SimaticML** → push vào TIA qua pipeline có sẵn.
> **Q4 đã chốt: dùng SimaticML + push có sẵn, KHÔNG viết XML tay.**

### Phần A — Generator mới `SiemensDbUdtGenerator` (C#)
**File tạo:** `src/GrafcetStudio.App/Generators/Siemens/SiemensDbUdtGenerator.cs`
- **Đọc trước:** `src/SimaticML/SimaticMLExamples.cs` (`CreateGlobalDBExample`, `CreateUDTExample`) + `src/GrafcetStudio.App/Generators/Siemens/SiemensLadDslGenerator.cs` (mẫu implement `ICodeGenerator` multi-file, cách `SanitizeBlockName`, `ToString(XmlDocument)`).
- Implement `GrafcetStudio.App.Generators.ICodeGenerator` (⚠️ đúng namespace `App.Generators`, KHÔNG nhầm `GrafcetStudio.CodeGen.ICodeGenerator`).
- `public string Platform => "siemens-db";` — **id riêng**, KHÔNG trùng `"siemens"` (`:57`) hay `"siemens-lad"` (`:58`), tránh đè key dict `CodeGeneratorService`.
- `GenerateFiles(CodegenPayload payload)`:
  - Duyệt `payload.Blocks` (Phần B). Mỗi block:
    - `kind=="DB"` → `new BlockGlobalDB()`, `.Init()`, `.AttributeList.BlockName = SanitizeBlockName(block.Name)`. Thêm biến qua **`AttributeList.STATIC.AddMember(name, dataType)`**.
    - `kind=="UDT"` → `new BlockUDT()`, `.Init()`, set BlockName. Thêm biến qua **`AttributeList.NONE.AddMember(...)`** (⚠️ KHÔNG `AddVariable` — ném cho NONE).
  - Chọn biến của block: `payload.Variables` có `BlockId == block.Id` **và** `DeclarationMode == "SymbolicBlock"`.
  - Map `dataType` (string) → `SimaticDataType` (tra static instances; nếu là tên UDT khác → nested: `AddMember(name, STRUCTURE)` rồi `.AddMember` con, dùng `StructLimits.MaxNestingDepth` từ Bước 5).
  - Mỗi block → 1 `CodegenFile { Path = "<BlockName>.xml", Content = ToString(SimaticMLAPI.CreateDocument(block)) }`.
  - Biến `AddressMapped`/không thuộc block → generator này **bỏ qua**.

**Đăng ký DI** `App.xaml.cs` (cạnh `:58`):
```csharp
containerRegistry.RegisterSingleton<ICodeGenerator, SiemensDbUdtGenerator>();
```

### Phần B — Payload đưa block/mode xuống backend
- **C# `CodegenPayload.cs`** — thêm field optional:
  ```csharp
  [JsonPropertyName("blocks")]
  public List<PlcBlock> Blocks { get; set; } = new();
  ```
- **Tạo DTO C# phẳng** `src/GrafcetStudio.App/Domain/Models/PlcBlock.cs` (chỗ duy nhất tạo class C# cho block — Q2):
  ```csharp
  using System.Collections.Generic;
  using System.Text.Json.Serialization;
  namespace GrafcetStudio.Domain.Models;
  public class PlcBlock
  {
      [JsonPropertyName("id")]           public string Id { get; init; } = string.Empty;
      [JsonPropertyName("name")]         public string Name { get; init; } = string.Empty;
      [JsonPropertyName("kind")]         public string Kind { get; init; } = string.Empty;
      [JsonPropertyName("memberVarIds")] public IList<string> MemberVarIds { get; init; } = new List<string>();
      [JsonPropertyName("comment")]      public string Comment { get; init; } = string.Empty;
  }
  ```
- **TS payload builder** `src/web/ts/codegen/payload.ts` — đính `blocks` vào payload C# (chỉ khi có; không thì bỏ trống → C# default `new()`). Tìm hàm `buildCSharpPayload` để thêm.

### Phần C — Parser import (TS)
**File:** `src/web/ts/editor/excel-import.ts`
- **Thêm hàm mới** `parseSiemensBlockCSV(rows, deviceTypes)` — KHÔNG sửa `parseStructCSV`/`parseUnitCSV`/`parsePhysicalIOCSV` cũ.
- Đọc cột `Name, DataType, [Comment], [Block]` → `ProjectVariable[]`: `declarationMode='SymbolicBlock'`, `blockId=<Block>` (default `''`), `kind='struct'` nếu `dataType` khớp DeviceType name, ngược lại `'primitive'`. **Không** parse address.
- Khai type `SiemensBlockCSVParseResult { vars: ProjectVariable[]; blocks: PlcBlock[]; errors: string[] }` trong `project.ts` (cạnh `StructCSVParseResult:322`), thêm hàm vào `ExcelImportApi` dưới key mới.

### Phần D — Push DB/UDT qua pipeline có sẵn
- Pipeline push (`ISiemensTiaProjectService.PushBlockXmlAsync`) **generic** — nhận XmlContent + BlockName, không phân biệt loại block. Dùng lại nguyên.
- ⚠️ **Nới `SiemensTiaPushOrchestrator.cs:134-140`:** hiện hard-code `platform=="siemens-lad"`. Để push DB, cho phép thêm `"siemens-db"`:
  - Sửa điều kiện guard `:135` thành chấp nhận cả `"siemens-lad"` và `"siemens-db"` (whitelist, KHÔNG bỏ guard). Gọi `codegen.Generate(platform, payload)` với `platform` truyền vào thay vì hard-code `"siemens-lad"` ở `:140`.
  - ⚠️ Đây là **sửa code đang chạy** — giữ nhánh `"siemens-lad"` hoạt động y nguyên, chỉ **mở rộng** whitelist. Verify luồng LAD cũ không đổi.
- **Web trigger** (`modal-host.ts`): thêm đường gọi push với `platform:'siemens-db'` (hoặc tham số target mới trong modal). ❗Nếu việc thêm UI target vượt phạm vi (đụng nhiều file modal) → **dừng, báo người dùng** trước khi làm UI.

### Ràng buộc bất biến
- Không sửa `SiemensLadDslGenerator`, `SimaticML`, `ISiemensTiaProjectService`, parser cũ.
- Không hardcode vendor trong code chung — rẽ nhánh qua `Platform` id `"siemens-db"`.
- `CodegenPayload.Blocks` optional (`= new()`) ⇒ payload cũ deserialize/generate bình thường.
- Orchestrator: chỉ **mở rộng** whitelist platform, giữ `"siemens-lad"` nguyên.

### Đồng bộ bắt buộc
- `PlcBlock` C# `[JsonPropertyName]` ↔ `interface PlcBlock` TS (Bước 3).
- `CodegenPayload.Blocks [JsonPropertyName("blocks")]` ↔ `blocks` do `payload.ts` đính.
- Field `BlockId`/`DeclarationMode` đọc trong generator ↔ tên JSON Bước 2/4.

### Tiêu chí done
- `dotnet build` + `npm run typecheck` pass.
- **Registry không vỡ:** app khởi động, dict `CodeGeneratorService` có đủ `"siemens"`, `"siemens-lad"`, `"siemens-db"` — không đè nhau. Verify: `grep -n "siemens-db\|SiemensDbUdtGenerator" src/GrafcetStudio.App/App.xaml.cs`.
- **Generate DB:** dựng payload có 1 `PlcBlock kind=DB` + vài biến `SymbolicBlock blockId=<db>` → `Generate("siemens-db", payload)` ra XML DB hợp lệ (đối chiếu `CreateGlobalDBExample`). Nested ≤3 cấp đúng.
- **Generate UDT:** tương tự với `kind=UDT`, dùng `NONE.AddMember`.
- **Push DB:** với `GRAFCETSTUDIO_TIA_IMPORT_MODE` phù hợp (hoặc mock/manual), orchestrator chấp nhận `"siemens-db"` không ném; luồng `"siemens-lad"` cũ **không đổi**.
- **Import CSV:** `parseSiemensBlockCSV` sinh biến `SymbolicBlock` + block, không đòi address.
- **Regression:** generate 1 project KV/Keyence + push LAD Siemens cũ — output/hành vi **không đổi**.

---

## ❗ Câu hỏi mới phát sinh từ hạ tầng thật — cần xác nhận TRƯỚC/ TRONG Bước 6

- **Q6 — Import UDT vào TIA (rủi ro đã xác minh).** Bridge V19 (`TiaV19ImportService`) import block vào `PlcBlockGroup.Blocks`. Trong Openness, **UDT nằm ở `PlcTypeGroup.Types`** — collection khác. ⇒ Push **DB** khả năng chạy; push **UDT** có thể KHÔNG được bridge hiện tại nhận.
  **Cần chốt:** lần này (a) chỉ làm **DB** cho luồng push (UDT chỉ generate ra file để import tay), hay (b) mở rộng cả `TiaBridge.V19` + `ReflectionSiemensTiaProjectService` để import UDT vào `Types`? (b) là việc lớn, đụng process bridge net48.

- **Q7 — Web UI target cho "siemens-db".** Hiện modal chỉ có luồng push `siemens-lad` (logic). Import variable→DB cần entry point UI mới (chọn block, chọn target DB/UDT). **Cần chốt:** Bước 6 làm luôn UI, hay chỉ làm backend generator + parser (giao/generate qua lệnh có sẵn), UI để phase sau?

- **Q8 — Nguồn của `PlcBlock` trên UI.** Người dùng tạo/gán block bằng cách nào ở phase này: (a) chỉ qua import CSV (`parseSiemensBlockCSV` tự sinh block), hay (b) cần UI quản lý block (tạo/sửa/xoá, gán biến)? Ảnh hưởng khối lượng Bước 3/6.

- **Q9 — Map `dataType` → `SimaticDataType`.** AI allow-list hiện là `['Bool','Int','Real','Word','DWord','Time']` (`ai/contracts.ts:247`). SimaticML `SimaticDataType` có `BOOLEAN/INT/WORD/DWORD/DINT/REAL/LREAL/...`. **Cần chốt:** danh sách kiểu dữ liệu hỗ trợ cho DB/UDT lần này = đúng allow-list trên, hay mở rộng thêm (LReal, DInt, String, Time...)? Kiểu lạ (tên UDT) xử lý như nested/tham chiếu.