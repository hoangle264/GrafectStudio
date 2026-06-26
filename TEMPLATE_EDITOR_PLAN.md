# Template Editor Plan

## Mục tiêu

Thêm tính năng **Template Editor** cho GrafcetStudio để người dùng có thể chỉnh sửa template Handlebars, với hai nhóm tính năng chính:

1. **Đổi màu chữ / Syntax Highlight**
   - Highlight cú pháp Handlebars.
   - Nhận diện biến `{{name}}`.
   - Nhận diện block `{{#each}}`, `{{#if}}`.
   - Nhận diện closing block `{{/each}}`, `{{/if}}`.
   - Nhận diện `{{else}}`.
   - Nhận diện partial `{{> partial}}`.
   - Có thể mở rộng cho comment `{{! comment}}`.

2. **Gợi ý Handlebars Keyword / Autocomplete**
   - Khi người dùng gõ `{{`, hiển thị danh sách gợi ý.
   - Hỗ trợ các gợi ý cơ bản:
     - `{{#each }}`
     - `{{/each}}`
     - `{{#if }}`
     - `{{/if}}`
     - `{{else}}`
     - `{{> partial}}`
     - `{{! comment }}`

> Ghi chú: Tài liệu này là plan thiết kế. Chưa triển khai code nếu chưa có yêu cầu riêng.

---

## Phạm vi bản đầu tiên

### Nên có

- Một màn hình hoặc modal **Template Editor**.
- Editor nhập template.
- Syntax highlight Handlebars cơ bản.
- Autocomplete keyword Handlebars.
- Nút lưu template.
- Nút hủy thao tác.
- Nút reset về template mặc định.

### Chưa bắt buộc ở bản đầu

- Preview output.
- Validation nâng cao.
- Import/export template.
- Quản lý nhiều template.
- Gợi ý biến từ context thật của project.
- Lưu template vào project file.

---

## Phase 1: Khảo sát hiện trạng project

Mục tiêu của phase này là xác định đúng vị trí và cách tích hợp Template Editor vào GrafcetStudio.

### Việc cần khảo sát

1. **Cấu trúc frontend**
   - File HTML chính nằm ở đâu?
   - File JavaScript chính nằm ở đâu?
   - Có modal system sẵn chưa?
   - CSS theme hiện tại tổ chức thế nào?
   - Frontend đang dùng module/bundler hay script thường?

2. **Luồng code generation hiện tại**
   - Template đang hard-code ở C# hay JavaScript?
   - Có thư mục template riêng không?
   - Generator nào đang dùng template?
   - Template có đang tách riêng khỏi logic generator không?

3. **Editor hiện tại nếu có**
   - Dùng `textarea` thường?
   - Dùng `contenteditable`?
   - Dùng Monaco?
   - Dùng CodeMirror?
   - Hay đang dùng custom editor?

### Kết quả cần có

Sau phase này cần trả lời được:

- Template Editor nên đặt ở đâu trong UI.
- Nên dùng editor library hay custom editor.
- Những file nào sẽ liên quan khi triển khai.
- Template sẽ được lưu ở frontend, project file hay file riêng.

---

## Phase 2: Chọn hướng kỹ thuật cho editor

Có ba hướng chính.

---

### Option A: Monaco Editor

Monaco là editor giống VS Code.

#### Ưu điểm

- Editor rất mạnh.
- Syntax highlight tốt.
- Autocomplete tốt.
- Có thể định nghĩa language riêng cho Handlebars.
- UX chuyên nghiệp.

#### Nhược điểm

- Bundle lớn.
- Cấu hình phức tạp hơn.
- Có thể hơi nặng nếu project hiện tại đơn giản.

#### Nên dùng nếu

- Project đã có npm build tốt.
- Muốn Template Editor trở thành tính năng lớn, lâu dài.
- Cần trải nghiệm chỉnh template chuyên nghiệp.

---

### Option B: CodeMirror

CodeMirror nhẹ hơn Monaco nhưng vẫn đủ mạnh.

#### Ưu điểm

- Nhẹ hơn Monaco.
- Hỗ trợ syntax highlight và autocomplete tốt.
- Dễ nhúng vào web UI.
- Phù hợp với editor dạng embedded trong app.

#### Nhược điểm

- Vẫn cần thêm dependency.
- Cần cấu hình extension.
- Cần kiểm tra project hiện tại có thuận tiện thêm package không.

#### Nên dùng nếu

- Project có thể cài npm package.
- Muốn editor tốt nhưng không quá nặng.
- Muốn autocomplete và highlight ổn định.

---

### Option C: Custom Editor

Tự làm bằng `textarea` kết hợp overlay highlight, hoặc dùng `contenteditable`.

#### Ưu điểm

- Không cần dependency mới.
- Chủ động hoàn toàn.
- Có thể làm nhanh cho bản thử nghiệm.

#### Nhược điểm

- Khó xử lý cursor và selection.
- Khó làm autocomplete mượt.
- Khó sync scroll nếu dùng overlay.
- Dễ phát sinh bug khi template dài.

#### Nên dùng nếu

- Không muốn thêm dependency.
- Chỉ cần bản đầu rất đơn giản.
- Chưa muốn đầu tư editor chuyên nghiệp.

---

## Khuyến nghị kỹ thuật

Nếu project hiện tại đã có npm build và frontend có cấu trúc tương đối rõ:

> Khuyến nghị dùng **CodeMirror**.

Lý do:

- Nhẹ hơn Monaco.
- Đủ mạnh cho Handlebars.
- Dễ triển khai autocomplete.
- Dễ style theo theme hiện tại.
- Phù hợp cho editor nằm trong modal/tab của app desktop/webview.

Nếu project không muốn thêm dependency:

> Làm bản đầu bằng `textarea` + logic autocomplete đơn giản, sau đó nâng cấp lên CodeMirror.

---

## Phase 3: Thiết kế UX

### Vị trí UI đề xuất

Có thể chọn một trong các vị trí sau:

1. **Modal Template Editor**
   - Mở từ nút trong CodeGen hoặc Settings.
   - Phù hợp nếu editor là tính năng phụ.

2. **Tab riêng trong CodeGen**
   - Phù hợp nếu template liên quan trực tiếp tới sinh code.
   - Người dùng dễ thấy mối liên hệ giữa template và output.

3. **Trang Settings / Generator Settings**
   - Phù hợp nếu template là cấu hình dài hạn.
   - Ít gây rối cho màn hình chính.

### Khuyến nghị UX

Bản đầu nên dùng:

> **Modal hoặc tab trong khu vực CodeGen**.

Nếu GrafcetStudio đã có modal system sẵn, dùng modal để ít ảnh hưởng layout hiện tại.

---

## UI đề xuất

```text
+------------------------------------------------------+
| Template Editor                                      |
+------------------------------------------------------+
| Template: [Unit Config Template v]                   |
|                                                      |
| +--------------------------------------------------+ |
| | {{#each outputs}}                                | |
| |   {{name}} = {{address}}                         | |
| | {{/each}}                                        | |
| +--------------------------------------------------+ |
|                                                      |
| [Reset to default]        [Cancel] [Save Template]   |
+------------------------------------------------------+
```

### Nếu có preview sau này

```text
+------------------------+---------------------------+
| Template               | Preview                   |
+------------------------+---------------------------+
| {{#each outputs}}      | Q0.0 = MotorStart          |
|   {{name}}             | Q0.1 = ValveOpen           |
| {{/each}}              |                           |
+------------------------+---------------------------+
```

---

## Phase 4: Syntax Highlight

### Token cần highlight

| Token | Ví dụ | Class gợi ý |
|---|---|---|
| Delimiter | `{{`, `}}` | `hbs-delimiter` |
| Block keyword | `#each`, `#if` | `hbs-keyword` |
| Closing keyword | `/each`, `/if` | `hbs-keyword-close` |
| Else keyword | `else` | `hbs-keyword` |
| Partial marker | `>` | `hbs-partial-marker` |
| Comment marker | `!` | `hbs-comment` |
| Variable/path | `name`, `unit.outputs`, `this.address` | `hbs-variable` |
| String literal | `"text"`, `'text'` | `hbs-string` |

### Các pattern cần nhận diện

```handlebars
{{name}}
{{unit.name}}
{{this.address}}
{{#each outputs}}
{{#if enabled}}
{{else}}
{{/each}}
{{/if}}
{{> header}}
{{! generated file }}
```

### Màu gợi ý

| Class | Mục đích | Gợi ý màu |
|---|---|---|
| `hbs-delimiter` | Dấu `{{ }}` | xám hoặc tím nhạt |
| `hbs-keyword` | `#each`, `#if`, `else` | xanh/tím nổi bật |
| `hbs-keyword-close` | `/each`, `/if` | đỏ/cam nhẹ |
| `hbs-variable` | biến | xanh dương hoặc trắng sáng |
| `hbs-partial-marker` | partial | vàng |
| `hbs-comment` | comment | xám/italic |
| `hbs-string` | string literal | xanh lá |

---

## Phase 5: Autocomplete

### Trigger autocomplete

Autocomplete nên mở khi:

1. Người dùng gõ `{{`.
2. Cursor đang nằm trong biểu thức Handlebars `{{ ... }}`.
3. Người dùng nhấn `Ctrl + Space`.

### Keyword cơ bản

| Label | Insert text |
|---|---|
| `#each` | `{{#each items}}\n  \n{{/each}}` |
| `#if` | `{{#if condition}}\n  \n{{/if}}` |
| `else` | `{{else}}` |
| `/each` | `{{/each}}` |
| `/if` | `{{/if}}` |
| `> partial` | `{{> partial}}` |
| `! comment` | `{{! comment }}` |

### Hành vi mong muốn

- Khi gõ `{{`, danh sách gợi ý xuất hiện.
- Khi chọn `#each`, editor tự chèn block đầy đủ.
- Cursor nên nằm ở vị trí cần sửa đầu tiên, ví dụ `items`.
- Nếu người dùng đang ở trong `{{ }}`, chỉ chèn phần keyword, tránh tạo lặp dấu `{{`.

Ví dụ:

Người dùng gõ:

```handlebars
{{
```

Chọn `#each`, kết quả:

```handlebars
{{#each items}}
  
{{/each}}
```

Nếu người dùng đang gõ:

```handlebars
{{#e
```

Chọn `#each`, có thể hoàn thành thành:

```handlebars
{{#each items}}
  
{{/each}}
```

---

## Phase 6: Gợi ý biến context

Đây là tính năng mở rộng sau bản đầu.

### Ví dụ biến có thể gợi ý

```text
unitName
deviceName
inputs
outputs
steps
transitions
actions
```

### Với block `each`

Nếu context có array-like fields, nên ưu tiên gợi ý:

```handlebars
{{#each inputs}}
{{#each outputs}}
{{#each steps}}
{{#each transitions}}
```

### Lưu ý

Không nên triển khai phần này trước khi biết rõ schema dữ liệu codegen hiện tại.

---

## Phase 7: Template storage

Cần quyết định template được lưu ở đâu.

---

### Option 1: localStorage

#### Ưu điểm

- Dễ triển khai.
- Không cần thay đổi backend.
- Phù hợp bản đầu.

#### Nhược điểm

- Chỉ lưu trên máy/ngữ cảnh webview hiện tại.
- Không đi theo project.
- Khó share template giữa nhiều người.

#### Phù hợp khi

- Template là tùy chỉnh cá nhân.
- Muốn có bản đầu nhanh.

---

### Option 2: Project file

#### Ưu điểm

- Template đi cùng project.
- Dễ chia sẻ project kèm template.
- Phù hợp nếu template là một phần cấu hình dự án.

#### Nhược điểm

- Cần cập nhật schema project.
- Có thể cần migration/versioning.
- Cần kiểm tra tương thích project cũ.

#### Phù hợp khi

- Template ảnh hưởng trực tiếp tới output của project.
- Người dùng cần share project cho team.

---

### Option 3: File template riêng

Ví dụ:

```text
templates/unit-config.hbs
templates/io-map.hbs
templates/header.hbs
```

#### Ưu điểm

- Rõ ràng.
- Dễ version control bằng git.
- Dễ import/export.
- Phù hợp power user.

#### Nhược điểm

- Cần file IO flow.
- Cần UI quản lý file template.
- Cần xử lý đường dẫn, quyền ghi, lỗi đọc file.

#### Phù hợp khi

- GrafcetStudio muốn hỗ trợ custom generator chuyên nghiệp.
- Người dùng cần quản lý nhiều template.

---

## Khuyến nghị storage

Bản đầu:

> Lưu trong frontend state + localStorage hoặc config nội bộ.

Bản sau:

> Hỗ trợ lưu theo project file hoặc export/import template.

---

## Phase 8: Validation

Validation giúp người dùng phát hiện lỗi template trước khi sinh code.

### Validation tối thiểu

1. Thiếu closing delimiter `}}`.
2. Block mở không có block đóng.
3. Block đóng không khớp block mở.
4. Closing block xuất hiện khi chưa có opening block.
5. Partial syntax không hợp lệ.

### Ví dụ lỗi

Sai vì thiếu closing block:

```handlebars
{{#each outputs}}
  {{name}}
```

Thông báo gợi ý:

```text
Line 1: Missing closing block for {{#each outputs}}
```

Sai vì đóng nhầm block:

```handlebars
{{#if enabled}}
  Enabled
{{/each}}
```

Thông báo gợi ý:

```text
Line 3: Expected {{/if}} but found {{/each}}
```

Sai vì thiếu `}}`:

```handlebars
{{name
```

Thông báo gợi ý:

```text
Line 1: Missing closing delimiter }}
```

### Mức độ ưu tiên

- Bản đầu: có thể chỉ validation nhẹ hoặc chưa cần.
- Bản tiếp theo: nên có validation block cơ bản.
- Bản hoàn chỉnh: validation hiển thị line number trong editor.

---

## Phase 9: Preview output

Preview là tính năng sau, nhưng nên thiết kế trước để không phải sửa nhiều.

### Chức năng

- Lấy sample data.
- Render template bằng Handlebars.
- Hiển thị output preview.
- Nếu render lỗi, hiển thị lỗi rõ ràng.

### UI gợi ý

```text
+------------------------+---------------------------+
| Template               | Preview                   |
+------------------------+---------------------------+
| {{#each outputs}}      | MotorStart = Q0.0          |
| {{name}} = {{address}} | ValveOpen = Q0.1           |
| {{/each}}              |                           |
+------------------------+---------------------------+
```

### Dữ liệu sample gợi ý

```json
{
  "unitName": "MainUnit",
  "outputs": [
    { "name": "MotorStart", "address": "Q0.0" },
    { "name": "ValveOpen", "address": "Q0.1" }
  ],
  "inputs": [
    { "name": "StartButton", "address": "I0.0" },
    { "name": "StopButton", "address": "I0.1" }
  ]
}
```

---

## Phase 10: Tích hợp với code generation

Sau khi editor ổn định, mới tích hợp vào code generation.

### Nguyên tắc

1. Nếu không có custom template, dùng default template.
2. Nếu có custom template, generator dùng custom template.
3. Nếu custom template lỗi, báo lỗi rõ ràng.
4. Người dùng có thể reset về default template.
5. Không làm hỏng luồng codegen hiện tại.

### Flow đề xuất

```text
User opens Template Editor
        |
        v
Load current template or default template
        |
        v
User edits template
        |
        v
Optional validation
        |
        v
Save custom template
        |
        v
Code generator uses custom template if enabled
```

---

## Checklist quyết định trước khi code

- [ ] Template Editor nằm ở đâu trong UI?
- [ ] Dùng CodeMirror, Monaco hay custom editor?
- [ ] Template lưu ở localStorage, project file hay file riêng?
- [ ] Có cần Preview ngay bản đầu không?
- [ ] Có cần validation block Handlebars ngay bản đầu không?
- [ ] Autocomplete chỉ keyword hay thêm biến context?
- [ ] Có hỗ trợ nhiều template hay chỉ một template?
- [ ] Có cần Reset to default không?
- [ ] Có cần Import/Export template không?
- [ ] Có cần tích hợp ngay vào code generator không?

---

## Roadmap đề xuất

### Version 1

- Thêm Template Editor modal/tab.
- Syntax highlight cơ bản.
- Autocomplete keyword Handlebars.
- Save template vào localStorage hoặc state.
- Reset to default.

### Version 2

- Validation block Handlebars.
- Preview output bằng sample data.
- Gợi ý biến từ context.

### Version 3

- Quản lý nhiều template.
- Import/export template.
- Lưu template theo project.
- Tích hợp sâu với code generator.

---

## Prompt làm việc liên tục

Có thể dùng prompt sau trong các lượt chat tiếp theo:

```text
Chúng ta đang thiết kế tính năng Template Editor cho GrafcetStudio, chưa code nếu tôi chưa yêu cầu.

Mục tiêu:
- Thêm editor cho template Handlebars.
- Có syntax highlight cho {{variable}}, {{#each}}, {{#if}}, {{else}}, {{/each}}, {{/if}}, {{> partial}}, {{! comment}}.
- Có autocomplete khi gõ {{ hoặc Ctrl+Space.
- Gợi ý keyword: {{#each}}, {{#if}}, {{else}}, {{/each}}, {{/if}}, {{> partial}}.
- Ưu tiên thiết kế rõ UX, kiến trúc, luồng lưu template, validation trước khi code.

Hãy làm việc theo từng phase:
1. Khảo sát cấu trúc project.
2. Đề xuất vị trí UI.
3. Chọn hướng editor: CodeMirror / Monaco / custom.
4. Thiết kế syntax highlight.
5. Thiết kế autocomplete.
6. Thiết kế lưu template.
7. Thiết kế validation.
8. Sau khi tôi xác nhận mới code.

Ở mỗi bước, hãy:
- Không code nếu tôi chưa yêu cầu.
- Nêu file/directory có thể liên quan.
- Nêu trade-off.
- Đề xuất phương án khuyến nghị.
- Đợi tôi xác nhận nếu có quyết định kiến trúc quan trọng.
```

---

## Quyết định cập nhật: Sử dụng TypeScript

Dự án GrafcetStudio hiện đã có một phần được chuyển sang TypeScript, vì vậy tính năng **Template Editor** nên được triển khai bằng **TypeScript** thay vì JavaScript mới.

### Kết luận

> Template Editor nên viết bằng **TypeScript**.

Nếu frontend build hiện tại hỗ trợ thêm dependency ổn định, hướng khuyến nghị là:

> **TypeScript + CodeMirror**

Nếu chưa muốn thêm dependency editor ngay từ đầu, vẫn nên viết module bằng TypeScript nhưng dùng editor đơn giản trước:

> **TypeScript + textarea/custom editor**, sau đó nâng cấp lên CodeMirror.

---

## Lý do chọn TypeScript

### 1. Dự án đã có nền TypeScript

Vì project đã có một phần chuyển sang TypeScript, khả năng cao đã tồn tại một số thành phần như:

- `tsconfig.json`
- npm build script hoặc build pipeline cho frontend
- các file `.ts`
- cơ chế bundle/compile TypeScript sang JavaScript

Do đó, thêm tính năng mới bằng TypeScript sẽ đồng nhất hơn với hướng phát triển hiện tại.

### 2. Template Editor có nhiều logic cần type rõ ràng

Template Editor không chỉ là một vùng nhập text. Tính năng này có thể bao gồm:

- template metadata
- syntax highlight
- autocomplete keyword
- autocomplete biến context
- validation diagnostics
- preview result
- storage provider
- import/export
- tích hợp code generation

Nếu viết bằng JavaScript, các object dữ liệu này dễ bị sai field hoặc sai kiểu tại runtime. TypeScript giúp phát hiện lỗi sớm hơn.

### 3. Dễ mở rộng với CodeMirror hoặc Monaco

Các editor library như CodeMirror và Monaco đều phù hợp với TypeScript. Những phần như:

- completion source
- language extension
- lint diagnostics
- editor state
- event/update listener

sẽ dễ bảo trì hơn nếu được viết bằng TypeScript.

### 4. Không cần migrate toàn bộ project ngay

Có thể viết riêng Template Editor bằng TypeScript, compile ra JavaScript, rồi expose API nhỏ cho phần JavaScript hiện tại gọi.

Ví dụ phần JavaScript legacy chỉ cần gọi:

```js
window.GrafcetTemplateEditor.open();
```

Trong khi toàn bộ logic editor vẫn nằm trong module TypeScript.

---

## Kiến trúc TypeScript đề xuất

Nên tách Template Editor thành module riêng, ví dụ:

```text
src/web/ts/template-editor/
  index.ts
  TemplateEditor.ts
  handlebarsCompletions.ts
  handlebarsHighlight.ts
  handlebarsValidation.ts
  templateStorage.ts
  templateTypes.ts
```

Nếu project đang có cấu trúc TypeScript khác, có thể đặt theo convention hiện tại. Nguyên tắc quan trọng là:

> Không viết toàn bộ Template Editor vào một file lớn.

---

## Vai trò từng file TypeScript

### `templateTypes.ts`

Chứa type/interface dùng chung cho Template Editor.

Ví dụ type cần có:

```ts
type TemplateKind = "unit-config" | "io-map" | "partial" | "custom";

type TemplateDiagnosticSeverity = "error" | "warning" | "info";

interface TemplateMetadata {
  id: string;
  name: string;
  kind: TemplateKind;
  isDefault: boolean;
  isCustom: boolean;
  updatedAt?: string;
}

interface HandlebarsCompletionItem {
  label: string;
  insertText: string;
  description?: string;
  kind: "block" | "keyword" | "partial" | "variable" | "comment";
}

interface TemplateDiagnostic {
  line: number;
  column: number;
  message: string;
  severity: TemplateDiagnosticSeverity;
}
```

---

### `handlebarsCompletions.ts`

Chứa logic autocomplete cho Handlebars.

Gợi ý cơ bản:

```text
#each
#if
else
/each
/if
> partial
! comment
```

Insert text tương ứng:

```handlebars
{{#each items}}
  
{{/each}}
```

```handlebars
{{#if condition}}
  
{{/if}}
```

```handlebars
{{else}}
```

```handlebars
{{/each}}
```

```handlebars
{{/if}}
```

```handlebars
{{> partial}}
```

```handlebars
{{! comment }}
```

Sau này có thể mở rộng autocomplete biến context:

```text
unitName
deviceName
inputs
outputs
steps
transitions
actions
```

---

### `handlebarsHighlight.ts`

Chứa cấu hình syntax highlight.

Nếu dùng CodeMirror:

- định nghĩa language/highlight extension
- định nghĩa style tag hoặc tokenizer phù hợp
- map token Handlebars sang theme class

Nếu dùng custom editor:

- chứa tokenizer hoặc regex parser đơn giản
- phân loại token thành delimiter, keyword, variable, partial, comment

Token cần nhận diện:

```handlebars
{{name}}
{{unit.name}}
{{this.address}}
{{#each outputs}}
{{#if enabled}}
{{else}}
{{/each}}
{{/if}}
{{> header}}
{{! generated file }}
```

---

### `handlebarsValidation.ts`

Chứa logic validate template.

Bản đầu có thể để placeholder hoặc validation nhẹ. Các rule nên hỗ trợ sau này:

- thiếu closing delimiter `}}`
- block mở không có block đóng
- block đóng không khớp block mở
- closing block xuất hiện khi chưa có opening block
- partial syntax không hợp lệ

Ví dụ output:

```ts
interface TemplateDiagnostic {
  line: number;
  column: number;
  message: string;
  severity: "error" | "warning" | "info";
}
```

---

### `templateStorage.ts`

Chứa logic lưu và đọc template.

Bản đầu có thể dùng:

```text
localStorage
```

Nhưng nên thiết kế qua interface để sau này đổi sang project file hoặc file riêng dễ hơn.

Ví dụ hướng thiết kế:

```ts
interface TemplateStorageProvider {
  loadTemplate(id: string): Promise<string | null>;
  saveTemplate(id: string, content: string): Promise<void>;
  resetTemplate(id: string): Promise<void>;
}
```

Storage về sau có thể thay bằng:

- project file
- template file riêng
- backend/WebView bridge service

---

### `TemplateEditor.ts`

Chứa logic chính để khởi tạo và điều khiển editor.

Nhiệm vụ:

- mount editor vào container
- load template hiện tại
- save template
- reset về default template
- bind event cho các button
- kết nối autocomplete
- kết nối syntax highlight
- gọi validation nếu có
- expose các method cần thiết cho UI

---

### `index.ts`

Entry point của module Template Editor.

Nhiệm vụ:

- export API nội bộ nếu dùng module import/export
- hoặc expose API ra `window` nếu phần JavaScript hiện tại cần gọi

Ví dụ API public:

```ts
interface GrafcetTemplateEditorApi {
  open(templateId?: string): void;
  close(): void;
  save(): Promise<void>;
  reset(): Promise<void>;
}
```

Có thể expose cho JavaScript legacy:

```ts
declare global {
  interface Window {
    GrafcetTemplateEditor: GrafcetTemplateEditorApi;
  }
}
```

---

## Cách tích hợp với JavaScript hiện tại

Vì project có thể vẫn còn JavaScript, Template Editor nên được viết bằng TypeScript nhưng chỉ expose API nhỏ để JavaScript hiện tại gọi.

Mô hình:

```text
TypeScript Template Editor module
        |
        v
Build/compile ra JavaScript bundle
        |
        v
JavaScript hiện tại gọi window.GrafcetTemplateEditor.open()
```

Ưu điểm:

- Không cần migrate toàn bộ app ngay.
- Không phá cấu trúc JavaScript hiện có.
- Logic mới vẫn được viết bằng TypeScript.
- Sau này dễ refactor/migrate tiếp.

---

## Cập nhật lựa chọn editor

Vì đã chọn TypeScript, lựa chọn editor nên ưu tiên như sau:

### Ưu tiên 1: TypeScript + CodeMirror

Nên chọn nếu:

- build frontend hỗ trợ npm dependency
- có thể thêm package CodeMirror
- muốn syntax highlight và autocomplete ổn định
- muốn mở rộng validation/preview sau này

Đây là hướng khuyến nghị chính.

### Ưu tiên 2: TypeScript + Monaco

Nên chọn nếu:

- muốn editor mạnh giống VS Code
- chấp nhận bundle lớn hơn
- app cần trải nghiệm editor chuyên nghiệp cao

Không phải lựa chọn đầu tiên nếu chỉ cần Template Editor nhẹ.

### Ưu tiên 3: TypeScript + custom textarea

Nên chọn nếu:

- chưa muốn thêm dependency
- muốn bản đầu nhanh
- chỉ cần autocomplete/highlight cơ bản

Nhược điểm là highlight và autocomplete sẽ khó mượt bằng CodeMirror.

---

## Quyết định roadmap sau cập nhật TypeScript

### Version 1

- Viết Template Editor bằng TypeScript.
- Tạo module riêng `template-editor`.
- Ưu tiên dùng CodeMirror nếu build hỗ trợ.
- Syntax highlight Handlebars cơ bản.
- Autocomplete keyword Handlebars cơ bản.
- Save/reset template.
- Storage tạm thời qua localStorage hoặc storage provider đơn giản.

### Version 2

- Thêm validation diagnostics.
- Thêm preview output bằng sample data.
- Thêm autocomplete biến context.

### Version 3

- Quản lý nhiều template.
- Import/export template.
- Lưu template theo project hoặc file riêng.
- Tích hợp sâu với code generator.

---

## Checklist cập nhật trước khi code

- [ ] Xác định thư mục TypeScript hiện tại trong project.
- [ ] Xác định build tool đang dùng cho TypeScript.
- [ ] Kiểm tra có thể thêm CodeMirror dependency không.
- [ ] Chốt dùng CodeMirror hay custom editor cho bản đầu.
- [ ] Chốt API public để JavaScript hiện tại gọi.
- [ ] Chốt storage bản đầu: localStorage, project file hay bridge service.
- [ ] Chốt vị trí UI: modal, tab CodeGen hay Settings.
