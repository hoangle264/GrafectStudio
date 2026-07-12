# Keyence Expression Template Refactor Plan

## Mục tiêu
Chuyển Keyence codegen từ template HBS kiểu mnemonic thấp tầng sang template **flow-based nhưng step body theo expression**, để file template như `auto.hbs` dễ viết và dễ bảo trì hơn.

- **Đơn vị trung tâm vẫn là flow**
- Template vẫn lặp theo `flow -> steps`
- Nhưng phần logic bên trong step không còn phải viết chi tiết kiểu:
  - `LD M0`
  - `AND M1`
  - `OUT M2`
- Thay vào đó, step sẽ được cấp dữ liệu ở mức cao hơn, ví dụ:
  - `M0 & M1`
  - `OUT M2`

---

## Điều chỉnh trọng tâm so với plan cũ
Plan cũ đang đi lạc sang hướng:
- parser shared generic
- Keyence instruction abstraction
- `KeyenceMnemonicExpressionEmitter`
- tích hợp vào `KeyenceMnemonicGenerator.cs`

Hướng đó **không phải trọng tâm của refactor này**.

Refactor này phải đặt trọng tâm vào:
- `KeyenceGenerator.cs`
- shape của context đưa vào template
- thay thế contract template cũ bằng contract step-expression mới

---

## Phạm vi đúng của đợt refactor này
### In scope
- Refactor `KeyenceGenerator.cs`
- Thiết kế lại model/context để template render step theo expression
- Giảm phụ thuộc vào template/partial legacy
- Chuẩn bị dữ liệu để `auto.hbs` mới viết ngắn hơn, dễ hiểu hơn

### Out of scope
- Không lấy `KeyenceMnemonicGenerator.cs` làm trung tâm triển khai
- Không ưu tiên xây emitter mnemonic C# như deliverable chính
- Không mở rộng toàn bộ instruction model tương lai ngay trong đợt này
- Không dùng shared parser/generalized expression infrastructure nếu chưa thật sự cần cho template contract mới

---

## Các file cần chỉnh trọng tâm
### 1. `src/GrafcetStudio.App/Generators/KeyenceGenerator.cs`
Đây là file trọng tâm cần sửa.

Các vùng cần refactor:
- `SectionTemplateOrder`
- `KnownPartials`
- `RegisterPartials()`
- `BuildContext(...)`
- `BuildResolvedFlow(...)` (nếu cần bổ sung step view model)
- `BuildDeviceOutputGroups(...)`

### Mục tiêu sửa trong file này
- Giữ flow là trục chính
- Bổ sung step-level data phục vụ expression template
- Giảm dữ liệu buộc template phải tự ráp logic thấp tầng
- Chuẩn bị shape mới cho `auto.hbs`

---

## Những phần của plan cũ nên loại bỏ
Các phần dưới đây **không nên tiếp tục là deliverable chính** của plan mới:

### Loại bỏ khỏi trọng tâm triển khai
- `src/GrafcetStudio.App/Generators/KeyenceMnemonicGenerator.cs` là điểm tích hợp chính
- `src/GrafcetStudio.App/Generators/Keyence/KeyenceMnemonicExpressionEmitter.cs`
- `src/GrafcetStudio.App/Generators/Keyence/KeyenceInstructionModel.cs`
- shared extraction kiểu nặng cho parser nếu mục tiêu chỉ là làm template Keyence dễ viết hơn
- các milestone xoay quanh mnemonic emitter C#

### Lý do loại bỏ
Các phần trên giải quyết bài toán:
- emit mnemonic bằng C#

Trong khi bài toán hiện tại là:
- refactor template contract để người dùng viết HBS dễ hơn

---

## Hướng sửa code dự kiến
### Phase 1 - Refactor contract của Keyence template
Trong `KeyenceGenerator.cs`:
- xem lại `SectionTemplateOrder` vì đang khóa cấu trúc template cũ (`uc.error`, `uc.manual`, `uc.origin`, `uc.auto`)
- xem lại `KnownPartials` và `RegisterPartials()` vì đang ép template phụ thuộc nhiều partial legacy
- thiết kế lại dữ liệu step để template không phải tự ráp mnemonic chi tiết

### Kết quả mong muốn của phase này
Mỗi step trong flow nên có dữ liệu để template mới render kiểu expression, ví dụ ở mức ý niệm:
- condition expression
- output instruction
- output target
- các expression phục vụ action/done/transition nếu cần

> Không cần template phải tự đi từ `LD/AND/OR` thấp tầng nữa.

---

## Phase 2 - Reshape dữ liệu output/device theo hướng dễ render
Trong `BuildDeviceOutputGroups(...)`:
- giảm shape thiên về binding legacy
- ưu tiên shape dễ tiêu thụ trong template mới
- tránh để template phải tự nối `SourceExecuteBitRefs`, `SourceDoneBitRefs`, `AggregationMode` thành logic hoàn chỉnh

### Mục tiêu
Template chỉ cần đọc dữ liệu gần với ý định người dùng:
- điều kiện gì
- output gì
- áp vào target nào

---

## Phase 3 - Thay template cũ bằng template mới
Sau khi `KeyenceGenerator.cs` đã cấp đúng context:
- thay `auto.hbs` cũ bằng template flow-based mới
- step body viết theo expression form
- template ngắn hơn, ít partial hơn, dễ bảo trì hơn

---

## Những gì không nên làm trong đợt này
- Không dồn effort chính vào `KeyenceMnemonicGenerator.cs`
- Không tạo thêm abstraction lớn chỉ để emit mnemonic C# nếu template mới chưa cần
- Không giữ mọi cấu trúc partial cũ rồi chỉ vá thêm helper
- Không mở rộng plan sang tất cả output instruction tương lai nếu chưa phục vụ trực tiếp template refactor

---

## Kết luận
Refactor này phải được hiểu là:

> **giữ flow là đơn vị trung tâm, nhưng đổi step body từ mnemonic-level sang expression-friendly template model**

Vì vậy:
- file trọng tâm là **`KeyenceGenerator.cs`**
- template cũ như `auto.hbs` sẽ được thay sau khi generator cấp đúng context mới
- những phần từng được plan cũ tạo ra cho hướng mnemonic-emitter nếu không phục vụ mục tiêu template mới thì **phải loại bỏ khỏi plan**
