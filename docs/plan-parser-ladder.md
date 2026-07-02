# Plan: Parser biểu thức Ladder (String → Object Tree) — dùng thư viện có sẵn

## Mục tiêu
Chuyển biểu thức phẳng dạng:
```
segment.Powerrail & (contactParts[0] & (...)) & coil1 & coil2
```
thành object cây (AND/OR/TAG) để sinh XML cho TIA Openness.

Nguồn: file `.hbs` (dùng chung Keyence) → Handlebars render ra chuỗi → parser xử lý chuỗi này.

**Thay đổi so với plan gốc:** dùng thư viện parser combinator (Sprache hoặc Pidgin, NuGet, miễn phí) thay vì tự viết tokenizer + recursive descent parser. Gộp được Giai đoạn 1–3 cũ thành 1 bước.

---

## Giai đoạn 1 — Chọn thư viện & định nghĩa grammar
- Chọn Sprache (dễ đọc, gần BNF, phù hợp shipping nhanh) hoặc Pidgin (performance tốt hơn, phù hợp nếu parser sẽ sống lâu và mở rộng thêm toán tử). Với scope hiện tại (chỉ AND/OR/ngoặc/TAG), **Sprache là đủ tốt**.
- Định nghĩa grammar ngay trong code bằng combinator: token biến, `&`, `|`, `(`, `)`.
- Xác định độ ưu tiên: `&` cao hơn `|`, ngoặc ưu tiên cao nhất.
- Formalize grammar bằng BNF ngắn để tránh ambiguity khi viết combinator:
```
Expr     := OrExpr
OrExpr   := AndExpr ('|' AndExpr)*
AndExpr  := Primary ('&' Primary)*
Primary  := TAG | '(' Expr ')'
```
- **Định nghĩa rõ lexical rule cho TAG** (điểm dễ bị bỏ sót, dễ gây lỗi khó debug về sau):
  - Whitelist ký tự hợp lệ: chữ cái, số, `_`, `.`, `[`, `]`
  - Không cho phép khoảng trắng bên trong TAG
  - Ví dụ hợp lệ: `contactParts[0]`, `segment.Powerrail`, `foo.bar[12]`
- **Xử lý whitespace tường minh**: parser phải chấp nhận cả `a&b`, `a & b`, `a   &   b`, `( a | b ) & c` như nhau.

**Output:** grammar viết bằng thư viện + BNF tham chiếu, thay thế tài liệu BNF riêng.

---

## Giai đoạn 2 — Map ra Object Model
- Không cần tokenizer/parser riêng — thư viện tự lo việc quét chuỗi.
- Viết phần "map" kết quả parse ra object tree đã có sẵn:
```json
{ "type": "AND", "nodes": [ ... ] }
{ "type": "OR", "nodes": [ ... ] }
{ "type": "TAG", "ref": "..." }
```
- **Thêm bước normalize AST** sau khi parse xong (giúp XML generator phía sau dễ dùng hơn):
  - `AND`/`OR` luôn có `nodes.length >= 2`
  - `TAG` là leaf duy nhất
  - Flatten các node cùng loại lồng nhau: `a & (b & c)` → `AND[a,b,c]` (không giữ dạng nhị phân lồng)
  - Bỏ ngoặc dư không ảnh hưởng cấu trúc

**Output:** hàm `Parse(string expr) -> ExpressionNode` (đã normalize).

---

## Giai đoạn 3 — Kiểm thử (Testing)
Test chia theo nhóm rõ ràng, tối thiểu 8-12 case:

**A. Happy path cơ bản**
- `a & b`, `a | b`, `(a | b) & c`

**B. Precedence (nhóm quan trọng nhất — dễ "đúng cú pháp nhưng sai logic")**
- `a | b & c` phải parse thành `OR[a, AND[b,c]]`
- `a & b | c` phải parse thành `OR[AND[a,b], c]`

**C. Ngoặc / nested**
- `((a))`
- `(a & (b | c)) & d`
- Ví dụ thực tế nhiều lớp (dùng làm test case chính)

**D. Tên biến / TAG syntax**
- `contactParts[0]`, `segment.Powerrail`, `foo.bar[12]`
- Có khoảng trắng quanh token/toán tử

**E. Trường hợp lỗi**
- Chuỗi rỗng, `a &`, `(a | b` (thiếu ngoặc đóng), `a $$ b` (ký tự lạ), `()`

**Output:** bộ unit test xUnit — 8-12 case, bắt buộc có 2-3 test về precedence.

---

## Giai đoạn 4 — Tích hợp
- Nối vào pipeline hiện tại: `.hbs` → Handlebars render chuỗi → `Parse()` (thư viện) → object tree (đã normalize) → dùng lại code XML generator đã có.
- Không đổi phần sinh XML, chỉ thay nguồn object đầu vào.
- **Cần xác nhận trước khi tích hợp:**
  - Chuỗi Handlebars render ra có "sạch" không (newline, extra space, token rỗng)?
  - Template có khả năng sinh dư ngoặc/dư toán tử không?
  - XML generator hiện tại có đang giả định cấu trúc nhị phân (`AND(left, right)`) không? Nếu có, cần viết adapter mỏng để khớp với `AND[nodes...]` — đây là chỗ dễ phát sinh effort ngoài dự kiến.

---

## Acceptance criteria (định nghĩa "done")
- Parse đúng tất cả case mẫu thực tế đã biết.
- Bảo toàn đúng precedence: `()` > `&` > `|`.
- Hỗ trợ đúng token dạng `segment.Powerrail`, `contactParts[0]`.
- Error message có vị trí và mô tả ngắn gọn (ví dụ: `Invalid token '$' at position 4`).
- XML generator chạy được không cần sửa, hoặc chỉ cần adapter mỏng.

---

## Ước lượng thời gian

| Giai đoạn | Thời gian |
|---|---|
| 1. Chọn thư viện + grammar (kể cả rule TAG) | 0.25-0.5 ngày |
| 2. Map object model + normalize AST | 0.25-0.5 ngày |
| 3. Testing (gồm test precedence riêng) | 0.5 ngày |
| 4. Tích hợp + verify XML output | 0.5-0.75 ngày |
| **Tổng (best case)** | **~1.5 ngày** |
| **Tổng (safe estimate, có buffer)** | **~2 ngày** |

*Giữ buffer tới ~2 ngày nếu pipeline XML hiện tại chưa tách biệt rõ với phần logic parse, hoặc XML generator đang giả định cấu trúc nhị phân.*

---

## Rủi ro cần lưu ý khi feedback với Codex
- **Sai precedence khi viết combinator (rủi ro cao nhất về logic):** parser vẫn có thể parse "thành công" nhưng AST sai — bắt buộc phải có test precedence riêng để khóa lỗi này.
- **Tên biến chứa ký tự đặc biệt** (`[`, `]`, `.`) dễ bị hiểu nhầm nếu rule TAG viết ngây thơ — xử lý được dễ dàng nếu whitelist ký tự rõ ràng ngay từ đầu.
- **Thông báo lỗi mặc định của thư viện khó đọc** (dạng vị trí ký tự) — nên bọc lại thành message rõ ràng hơn, kiểu `Invalid expression at position 12: expected ')'`, tránh crash âm thầm.
