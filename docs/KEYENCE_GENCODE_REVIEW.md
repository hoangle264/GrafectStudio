# Đánh giá gencode Keyence (KV Studio mnemonic)

Tài liệu ghi lại các vấn đề của pipeline sinh code Keyence (`.mnm`) trên nhánh `main`, kèm hướng xử lý. Mục tiêu là làm nguồn tham chiếu khi sửa, không phải một bản đặc tả.

Pipeline hiện tại:

```
FlowInfo (Grafcet)
  → SequenceResolver           (Domain/Resolution/SequenceResolver.cs)
  → GeneratorContextBuilder    (Generators/Common/GeneratorContextBuilder.cs)
  → template .hbs              (templates/, nạp bởi TemplateManager)
  → text pseudo "A & B -> OUT X"
  → MnemonicEmitter            (Generators/Keyence/MnemonicEmitter.cs)
  → file .mnm
```

Các mục đánh dấu **[đã kiểm tra]** là đã đọc code xác nhận. Mục **[cần kiểm tra]** là suy luận hoặc phụ thuộc vào hành vi KV Studio/dữ liệu thật, chưa xác nhận.

---

## 1. Grafcet bị duỗi thành chuỗi thẳng — rẽ nhánh sinh sai  [đã kiểm tra]

**Vị trí:** [SequenceResolver.cs:21-26](../src/GrafcetStudio.App/Domain/Resolution/SequenceResolver.cs#L21-L26)

Thuật toán chỉ đi theo connection **đầu tiên** của mỗi step:

```csharp
var transitionId = state.Connections.FirstOrDefault(c => c.From == current.Id)?.To;
var nextStepId   = state.Connections.FirstOrDefault(c => c.From == transitionId)?.To;
```

Các step không đi tới được bị nối vào cuối danh sách theo số thứ tự.

**Hệ quả:**

| Loại lưu đồ | Hành vi hiện tại |
|---|---|
| Chuỗi thẳng | Đúng |
| Rẽ nhánh OR (S2 → T2a→S3, T2b→S5) | Chỉ có nhánh T2a; T2b bị mất hoàn toàn |
| Song song AND (parallel bar) | Vòng lặp dừng ở bar (`byId` không có id của bar), các nhánh bị xếp tuần tự |
| Nhảy ngược về step đầu | `NextStep` của step cuối là step **giả** (địa chỉ +1, [GeneratorContextBuilder.cs:203](../src/GrafcetStudio.App/Generators/Common/GeneratorContextBuilder.cs#L203)), không trỏ về step đầu |

- `ResolvedStep` chỉ có một `NextStep` và một `OutTransition`, nên template không biểu diễn được nhiều lối ra.
- `BranchType` (`ParallelSplit` / `ParallelJoin`) được tính trong [SequenceResolver.cs:65-72](../src/GrafcetStudio.App/Domain/Resolution/SequenceResolver.cs#L65-L72) nhưng không template nào dùng.
- Phía web có model parallel bar ([graph-utils.ts:58](../src/web/ts/core/graph-utils.ts#L58)), còn phía C# không có model tương ứng.
- Không có cảnh báo nào được phát ra; file vẫn được sinh.

**Hướng xử lý:** sinh code theo **từng transition** thay vì theo chuỗi step:

```
; T2b : S2 → S5 (OR: mỗi transition là một rung riêng)
LD   S2
AND  <điều kiện T2b>
SET  S5
RES  S2

; T4 : S3, S4 → S6 (AND-join: chờ đủ các step trước)
LD   S3
AND  S4
AND  <điều kiện T4>
SET  S6
RES  S3
RES  S4
```

Cần thêm model parallel bar phía C# để biết bar nối những step nào.

---

## 2. Code đi qua hai lớp text  [đã kiểm tra]

**Cơ chế:** Handlebars render ra dạng pseudo `MR300 & R1001 & R1003 -> SET MR301`, sau đó [MnemonicEmitter.cs:51](../src/GrafcetStudio.App/Generators/Keyence/MnemonicEmitter.cs#L51) tách lại từng dòng tại `->` và parse điều kiện để sinh mnemonic.

Chương trình tạo text rồi parse ngược lại text đó, trong khi dữ liệu ban đầu đã có cấu trúc. Các vấn đề sau đều bắt nguồn từ đây.

### 2a. Escape HTML
Handlebars mặc định escape giá trị `{{...}}`: `&` → `&amp;`, `>` → `&gt;`. Code phải `HtmlDecode` ([MnemonicEmitter.cs:75](../src/GrafcetStudio.App/Generators/Keyence/MnemonicEmitter.cs#L75)). Đây là vá chỗ hỏng, không phải thiết kế đúng. Có thể làm sai nếu một tên biến thật chứa ký tự cần escape.

### 2b. Dòng hỏng được xử lý không nhất quán  [đã kiểm tra]
- Dòng **sai hình dạng** (thiếu `->`, thiếu lệnh, lệnh không phải OUT/SET/RST/RES): `TryConvertPseudoExpressionLine` trả `false` và dòng được **giữ nguyên** trong `.mnm`. Lỗi chỉ lộ ra khi import vào KV Studio.
- Dòng **đúng hình dạng nhưng điều kiện hỏng** (ví dụ `MR300 & & R1003 -> SET MR301` do thiếu `Complete.Address`): parser ném `LogicExpressionParseException` và toàn bộ lần gencode thất bại. Thông báo không cho biết step hay template nào gây lỗi.

Cả hai trường hợp đều khó truy nguyên.

### 2c. Hai kiểu template trộn lẫn  [đã kiểm tra]
- [templates/unitAuto.hbs](../templates/unitAuto.hbs) và [unitOrigin.hbs](../templates/unitOrigin.hbs): pseudo `-> OUT/SET`.
- [templates/devices/motor.hbs](../templates/devices/motor.hbs), [simple/Error.hbs](../templates/simple/Error.hbs): mnemonic thật `LD / ANB / OUT`.
- Generator chỉ chạy bước 2 nếu **toàn bộ output** có `->` ([KeyenceGenerator.cs:79](../src/GrafcetStudio.App/Generators/KeyenceGenerator.cs#L79)). Quy ước này ngầm, người viết template có thể vô tình phá vỡ.

### 2d. Fallback xuất JSON vào `.mnm`  [đã kiểm tra]
Nếu không section nào render ra được ([KeyenceGenerator.cs:68](../src/GrafcetStudio.App/Generators/KeyenceGenerator.cs#L68)), file `.mnm` chứa JSON của context. File trông như đã sinh xong nhưng vô dụng, và không có lỗi nào được báo.

**Hướng xử lý:** sinh đối tượng có cấu trúc (`Rung { Điều kiện, Lệnh, Comment }`) rồi mới in mnemonic. Bỏ hoàn toàn bước parse text. Xem mục 4.

---

## 3. Logic điều khiển nằm trong template  [đã kiểm tra một phần]

Interlock, Auto/Manual, xử lý lỗi được viết bằng `{{#if}}` trong `.hbs`, nên không unit test được và khó tái sử dụng.

Giá trị cứng trong template:
- Địa chỉ hệ thống `CR2002`, `CR2003`, `CR2006` trong [simple/DeviceManager.hbs](../templates/simple/DeviceManager.hbs) và [simple/System.hbs](../templates/simple/System.hbs).
- Tên biến `IN_bEMG1..4`, `HMI_nError1`… trong [simple/System.hbs](../templates/simple/System.hbs).

Các điểm nghi ngờ sai logic (cần xác nhận lại với người viết template):
- [simple/Error.hbs](../templates/simple/Error.hbs): hai nhánh `{{#if IsFirst}}` và `{{else}}` có nội dung giống nhau. Với nhiều cylinder, các lệnh `LD` nối tiếp không có `ORL`, có thể làm mất điều kiện lỗi của thiết bị trước.
- [unitAuto.hbs](../templates/unitAuto.hbs): step đầu xuất `NextStep.ExecAddress`, các step sau xuất `ExecAddress` của chính nó. Với `OUT` (không phải `SET/RES`), step không tự giữ trạng thái.
- `CR2003` được dùng làm điều kiện khởi động step đầu. **[cần kiểm tra]** Nhận xét cho rằng đây là bit luôn OFF trên KV; cần đối chiếu tài liệu KV để xác nhận, có thể là placeholder cố ý.
- [simple/UnitCode.hbs](../templates/simple/UnitCode.hbs) viết bằng cú pháp ST (`IF ... THEN`), khác với mnemonic của các template còn lại. **[cần kiểm tra]** Có đang được dùng không.

---

## 4. Lỗi template bị nuốt im lặng  [đã kiểm tra]

- Handlebars không bật strict mode. Gõ sai tên biến (`{{fwdAddr}}` thay vì `{{fwdAddr}}` đúng tên) cho chuỗi rỗng, và dòng thành `LD   ; Auto`.
- Context trộn chữ thường với PascalCase: `unit.label` nhưng `ExecAddress`, `NextStep`. Dễ gõ nhầm.

---

## 5. Kiểm thử mỏng  [đã kiểm tra]

- `KeyenceGeneratorBaselineTests`: 1 test.
- `KeyenceInstructionModelTests`: 3 test.
- `KeyenceMnemonicExpressionEmitterTests`: 9 test.
- Chưa có test nào kiểm tra **hành vi** của code sinh ra (ví dụ "bật input X, scan N vòng, step 3 phải active").

---

## Đề xuất nâng cấp theo thứ tự

### Ngắn hạn (vài ngày)
1. Thêm bước lint cho output: mỗi dòng phải là mnemonic hợp lệ hoặc comment. Còn `->` hoặc toán hạng rỗng thì đưa vào `warnings` và hiện trên UI, không xuất file lỗi. Có thể bật `ThrowOnUnresolvedBindingExpression` của Handlebars.Net để bắt biến sai tên.
2. Bỏ fallback xuất JSON vào `.mnm`; thay bằng lỗi rõ ràng.
3. Sửa các điểm nghi ngờ ở mục 3 sau khi xác nhận.
4. Gom địa chỉ hệ thống (`CR2002`…) và tên biến cố định vào `profile` thay vì hard-code.

### Trung hạn (quan trọng nhất): thay lớp text bằng IR

```
Grafcet graph ──► GrafcetCompiler (C#) ──► IR: List<Rung{ LogicExpression, Instruction, Comment }>
                                              │
                         ┌────────────────────┼─────────────────────┐
                    Keyence mnemonic     Siemens LAD/SCL       ST / ABB ...
```

- `GrafcetCompiler` làm việc trên đồ thị, không duỗi thành chuỗi (giải quyết mục 1). Mỗi transition sinh một rung: `(các step trước đều active) & điều kiện → SET các step sau, RES các step trước`.
- Template `.hbs` chỉ giữ phần bố cục, comment, header, khối thiết bị do người dùng tùy biến.
- Phần lớn đã có sẵn (`LogicExpression`, `KeyenceInstruction`, `KeyenceMnemonicExpressionEmitter`), cần ghép lại thành IR.
- IR dùng chung được với pipeline Siemens, tránh hai bộ logic song song.

### Dài hạn: kiểm thử theo hành vi
- Viết một bộ mô phỏng mnemonic nhỏ (stack machine cho `LD/AND/OR/ANB/ORL/MPS/MPP/OUT/SET/RES`, khoảng 200 dòng). Test dạng "bật input X, scan N vòng, step 3 phải active".
- **[cần kiểm tra]** Cân nhắc xuất ST cho KV-8000 nếu phiên bản KV STUDIO đang dùng hỗ trợ ST. Có IR thì thêm backend ST khá rẻ. Mnemonic vẫn giữ cho các dự án cũ.

---

## Kết luận

Nên giữ Handlebars cho phần tùy biến, nhưng không để Handlebars gánh logic Grafcet. Việc có giá trị nhất là **compiler theo đồ thị cộng với IR**: nó sửa lỗi rẽ nhánh (mục 1) và bỏ được lớp parse text dễ vỡ (mục 2), đồng thời làm cho logic testable được.

## Việc còn mở
- Xác nhận hành vi `CR2003` và các biến hệ thống trên KV-8000 với tài liệu KV STUDIO.
- Xác nhận `simple/UnitCode.hbs` (cú pháp ST) có đang được dùng không.
- Xác nhận phiên bản KV STUDIO và có hỗ trợ ST không.
- Bổ sung test hành vi trước khi sửa rẽ nhánh, để có mốc so sánh.
