# Plan tổng thể: Expression → Keyence Mnemonic

## Mục tiêu

KeyenceGenerator + HBS hiện in pseudo-expression (`A & !B`, `SET x`) → chuyển thành mnemonic PLC Keyence (LD/AND/OR/INV + OUT/SET/RST) dùng 3 lớp sẵn có trong `Generators/Keyence/`.

## Hiện trạng

```
Step/Action/Output
  → Build*Expression (string logic)
  → context HBS
  → render text pseudo
  → PLC không hiểu
```

3 lớp Keyence đã có + test unit, nhưng chưa wire vào generator.

## Kiến trúc đích

```
Domain data (step/action/output)
        │
        ▼
KeyenceGenerator
  build condition + instruction intent
        │
        ├─► KeyenceInstructionModel      (OUT/SET/RST/RES/FB)
        ├─► ExpressionEmitter            (LD/AND/OR/INV…)
        └─► InstructionEmitter           (OUT/SET…)
        │
        ▼
Context mnemonic sẵn
  activationMnemonic
  doneMnemonic
  action.mnemonic
  output.mnemonic
  bodyMnemonics[]
        │
        ▼
HBS chỉ layout / in text đã emit
```

**Nguyên tắc:** HBS không parse `& | !`. C# emit xong → template chỉ print.

## Phạm vi theo phase

### Phase 0 — Chuẩn bị (nhỏ)

- Xác định field expression đang feed template
- Inventory template HBS đang dùng field nào (`activationExpression`, `doneExpression`, `outputExpression`, …)
- Chốt contract context mới (giữ field cũ + thêm mnemonic, hoặc migrate dần)

**Deliverable:** map expression field → mnemonic field

### Phase 1 — Core wire step logic (ưu tiên)

Emit mnemonic cho vòng step cơ bản:

| Rung | Condition | Instruction |
|---|---|---|
| Activate | `activationExpression` | SET exec |
| Done | `doneConditionExpression` | SET done |
| Action | `step.Exec` (+ complete nếu có) | OUT/SET/RST target |
| Direct output | `conditionExpression` | OUT/SET/... target |

Việc code:
- Helper trong `KeyenceGenerator` (hoặc class cạnh): `EmitMnemonic(condition, instructionType, target, vars)`
- Map "OUT"/"SET"/"RST"/"RES" → `KeyenceInstructionType`
- Mở rộng context:
  - `StepExpressionContext`: `activationMnemonic`, `holdMnemonic?`, `doneMnemonic`, `bodyMnemonics`
  - `StepActionExpressionContext`: `mnemonic`, `mnemonicLines`
  - `StepOutputExpressionContext`: `mnemonic`, `mnemonicLines`
- Giữ field expression cũ tạm thời (compat template cũ)

**Deliverable:** generate step activate/done/action ra mnemonic đúng

### Phase 2 — Device output groups

Emit mnemonic cho `deviceOutputGroups` / command flows:
- `driveConditionExpression` + instruction/target
- mode flag + execute + done guard
- interlock expression

**Deliverable:** output section mnemonic, không còn `A & B ; OUT x`

### Phase 3 — Template HBS migrate

Đổi partial step/action/output từ:
```
{{expression}}
```
sang:
```
{{mnemonic}} / {{#each mnemonicLines}}
```

Update `templateContract`: `stepBody: "expression"` → `"mnemonic"`

Template cũ vẫn chạy nếu còn field expression (transition period)

**Deliverable:** output file PLC-ready từ pipeline thật

### Phase 4 — Edge cases / harden

- empty condition → skip / comment
- unsupported qualifier → `; [P] ... not implemented`
- compound OR/AND lồng nhau
- negate `!addr` / NOT
- operand resolve qua `DeviceVariable` / `AddressResolver`
- macro step / hold expression (nếu template cần)

### Phase 5 — Test + cleanup

- unit test helper emit trong generator context
- smoke test KeyenceGenerator snapshot/mnemonic fragments
- (optional) bỏ dần pseudo `BuildInstructionExpression` nếu template không còn dùng
- docs ngắn contract context mnemonic

## File dự kiến đụng

| File | Thay đổi |
|---|---|
| `Generators/KeyenceGenerator.cs` | wire emit, thêm field context |
| `Generators/Keyence/*` | reuse; sửa nhỏ nếu thiếu API |
| context models trong generator (nested classes) | thêm mnemonic fields |
| templates `*.hbs` (step/action/output) | print mnemonic |
| `GrafcetStudio.App.Tests/*` | test generator mnemonic |

Không nhét emit logic vào HBS helper nếu tránh được — giữ pure C#.

## API helper đề xuất

```
// trong KeyenceGenerator hoặc KeyenceMnemonicRenderer
string EmitRung(string condition, string instruction, string target, IList<DeviceVariable> vars)
IReadOnlyList<string> EmitRungLines(...)
KeyenceInstruction ToInstruction(string instruction, string target)
```

Nội bộ:
```
condition + instruction
  → KeyenceOutputInstruction.Create(...)
  → KeyenceMnemonicExpressionEmitter.EmitExpressionAndInstruction(...)
```

## Thứ tự làm thực tế

1. Phase 1 step activate/done/action
2. Verify bằng test + 1 sample payload
3. Phase 2 device outputs
4. Phase 3 sửa HBS
5. Phase 4–5 harden + cleanup

## Ngoài scope (chưa làm ngay)

- full FB body emission phức tạp
- timer/move/call custom instruction set đầy đủ
- đổi architecture template engine
- Siemens path

## Rủi ro

| Rủi ro | Cách xử lý |
|---|---|
| Template đang phụ thuộc pseudo expression | giữ dual field, migrate dần |
| Condition rỗng / invalid | guard + comment line |
| Format mnemonic lệch style cũ (padding) | dùng `PadOperands` option nếu cần |
| HBS expect 1 string 1 dòng | expose cả `mnemonic` (block text) + `mnemonicLines` |

## Kết quả mong đợi

Trước:
```
MR10 & !MR11 ; SET MR100
```

Sau:
```
LD   MR10
ANB  MR11
SET  MR100
```
