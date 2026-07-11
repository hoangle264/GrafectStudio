# Keyence Expression Support Plan

## Review Notes (2026-07-11, verified against feature/plc-variable-struct-redesign)
- Class names in Phase 1 are real (not guessed): `SiemensLadExpressionParser`, `SiemensLadExpressionParseException` (`Generators/Siemens/SiemensLadExpressionParser.cs`), `CollectRefs` (`Generators/Siemens/SiemensLadDslGenerator.cs:96`). Grammar matches: `Expr := OrExpr; OrExpr := AndExpr ('|' AndExpr)*; AndExpr := Primary ('&' Primary)*; Primary := TAG | '(' Expr ')'`.
- CORRECTION: `SiemensLadExpression` (the AST model) is NOT in the parser file — it's defined in `SiemensLadDslGenerator.cs:600`, decorated with `[JsonPropertyName]` for template JSON/HBS (de)serialization, alongside `SiemensLadNetwork`/`SiemensLadTemplate`/`SiemensLadOutput`. Extraction means physically moving this class out of the XML-emitting generator file, not just renaming references.
- CORRECTION: The tag-token grammar inside `SiemensLadExpressionParser.cs` (quoted components `"name"`, dotted paths, bit-slice suffix `.%X3`) is Siemens-specific and not needed for Keyence mnemonic addressing. The shared parser needs a pluggable/parameterized tag-token grammar (Siemens vs. Keyence token rules), not a single hardcoded one.
- CORRECTION: `KeyenceMnemonicGenerator.EmitConditionAndSet` (`Generators/KeyenceMnemonicGenerator.cs:47`) currently only maps N/S/R action qualifiers to OUT/SET/RST (lines 64-70). There is no FB or RES output anywhere in Keyence codegen today — these are net-new instructions to design, not existing behavior to preserve for backward compatibility.
- CORRECTION: No existing Keyence test file (`grep Keyence` in `GrafcetStudio.App.Tests` returns nothing). Phase 5's "regression coverage for old flow" must be written from scratch — there is no baseline to diff against, so budget more effort here than the plan implies.
- File target note: use `src/GrafcetStudio.App/GrafcetStudio.App.csproj` (correct spelling, references `Sprache`, is the project actually in `GrafectStudio.sln`). Do NOT use `GrafectStudio.App.csproj` (typo'd sibling file, no `Sprache` reference, not in the solution) — new Expressions/Keyence code must go into the former.
- Branch check: this Siemens infra (parser, AST, DSL generator, tests) is present on the current branch `feature/plc-variable-struct-redesign`, not just on `changeJavaScriptToTypeScript`. Confirm with `git ls-tree -r HEAD --name-only | grep -i siemens` before starting, since branches can diverge over time.

## Goal
Hỗ trợ parse và generate logic expression cho Keyence theo hướng tái sử dụng phần generic từ Siemens, với khả năng xử lý các biểu thức như:
A&B
A|B
(A|B)&C
Thiết kế phải mở rộng được trong tương lai cho các output/instruction khác, không khóa cứng chỉ vào:
OUT
SET
RST/RES
FB
Scope
In scope
Tái sử dụng parser/AST expression từ nhánh Siemens theo hướng shared
Tích hợp expression parsing vào generator Keyence
Emit mnemonic cho expression logic cơ bản
Hỗ trợ single-output instruction flow
Thiết kế abstraction để sau này thêm instruction mới
Out of scope for first phase
Full branch engine phức tạp
Multi-output network generation
Siemens XML/LAD output changes
Hỗ trợ toàn bộ instruction nâng cao ngay từ đầu
Architecture Direction
Shared layer
Tạo lớp logic chung, không phụ thuộc Siemens hay Keyence:
LogicExpression
LogicExpressionParser
LogicExpressionParseException
LogicExpressionWalker / helper
Siemens layer
Refactor Siemens để dùng shared parser/model
Giữ nguyên emitter Siemens-specific
Keyence layer
Tạo emitter riêng cho Keyence mnemonic
Generator Keyence chỉ consume shared AST rồi emit mnemonic tương ứng
Phase 1 — Extract Shared Expression Core
Objective
Tách phần parse expression khỏi Siemens để dùng chung.
Tasks
Xác định các thành phần generic trong Siemens:
parser (SiemensLadExpressionParser.cs — generic ngoại trừ tag-token grammar)
AST/model (SiemensLadExpression — thực ra nằm trong SiemensLadDslGenerator.cs:600, cần move ra, không chỉ rename)
parse exception
normalize logic (hiện là private Normalize() extension trong parser, không phải class Walker riêng)
collect refs helper (CollectRefs — hiện là private static trong SiemensLadDslGenerator.cs:96, cần move sang shared)
tag-token grammar: tách riêng phần Siemens-specific (quoted "name", bit-slice .%X3) khỏi phần generic, để Keyence có thể cắm token rule khác

Tạo shared module / namespace mới, ví dụ:
GrafcetStudio.App.Expressions
hoặc GrafcetStudio.App.Logic

Refactor các thành phần sau:
SiemensLadExpression → LogicExpression
SiemensLadExpressionParser → LogicExpressionParser
SiemensLadExpressionParseException → LogicExpressionParseException

Tách helper duyệt cây expression:
CollectRefs
normalize / flatten AND, OR

Cập nhật Siemens để dùng shared layer thay vì class Siemens-specific

Deliverables
Shared AST/model cho boolean expression
Shared parser với grammar &, |, (...)
Shared exception và helper utilities
Siemens compile lại được với shared parser
Phase 2 — Define Keyence Instruction Abstraction
Objective
Thiết kế model instruction cho Keyence đủ đơn giản để dùng ngay, nhưng mở rộng được sau này.
Tasks
Xác định structure instruction đầu ra cho Keyence:
output type
target ref/address
optional parameters
optional block/body metadata

Thiết kế model instruction trung gian, ví dụ:
KeyenceInstruction
KeyenceOutputInstruction
KeyenceInstructionType

Hỗ trợ trước các loại instruction hiện tại:
OUT
SET
RST / RES
FB
(Lưu ý: chỉ OUT/SET/RST tồn tại trong code hiện nay qua ActionQualifier N/S/R. RES và FB là instruction mới, cần thiết kế từ đầu chứ không phải giữ tương thích ngược.)

Thiết kế theo hướng extensible để sau này thêm:
timer
move
call
custom mnemonic instruction

Deliverables
Instruction abstraction dùng cho Keyence
Mapping rule cho 4 output hiện tại
Design note cho future instruction expansion
Phase 3 — Build Keyence Expression Emitter
Objective
Biến shared expression AST thành mnemonic logic cho Keyence.
Tasks
Thiết kế emitter riêng, ví dụ:
KeyenceMnemonicExpressionEmitter

Xây dựng flow xử lý:
parse expression string
normalize AST
collect refs
resolve operands
emit mnemonic condition sequence
append final instruction

Định nghĩa rule emit cho:
TAG
AND
OR
grouped expressions như (A|B)&C

Quyết định strategy implementation:
emit trực tiếp nếu expression đơn giản
hoặc cho phép strategy dùng intermediate relay/temporary result nếu cần sau này

Tách rõ:
expression emitter
output instruction emitter

Deliverables
Keyence expression emitter
Rule emit cho expression cơ bản
Khả năng kết hợp expression + single output instruction
Phase 4 — Integrate into Keyence Generator
Objective
Nối expression parsing/emitting vào luồng generate hiện có của Keyence.
Tasks
Xác định điểm tích hợp trong:
KeyenceMnemonicGenerator
và nếu cần, KeyenceGenerator

Thay logic resolve condition đơn giản hiện tại bằng pipeline mới:
lấy condition string
parse expression
emit Keyence mnemonic
append output instruction

Giữ backward compatibility cho:
condition đơn giản dạng 1 operand
condition rỗng / 1 / true

Bổ sung error handling:
parse syntax error
unknown operand
unsupported expression pattern
unsupported output instruction

Deliverables
Generator Keyence dùng được shared parser
Hỗ trợ expression logic trong condition
Backward-compatible với input cũ
Phase 5 — Validation and Test Coverage
Objective
Đảm bảo parser và emitter hoạt động đúng, dễ bảo trì và an toàn khi mở rộng.
Test groups
Parser tests
A
A&B
A|B
(A|B)&C
A&(B|C)
nested groups
syntax error cases:A|
|A
(A&B
A(B)
()

AST normalization tests
flatten nested AND
flatten nested OR
preserve grouping semantics
Keyence emitter tests
single tag + OUT
A&B + SET
A|B + RST
(A|B)&C + OUT
grouped expression + FB
Integration tests
condition from flow/transition parsed correctly
variable/address resolution works
generated mnemonic format remains valid
old simple conditions still behave as before
Future extensibility tests
thêm output type mới không cần đổi parser
emitter output layer có thể mở rộng độc lập
Deliverables
Parser unit tests
Emitter unit tests
Integration tests cho generator Keyence
Regression coverage cho luồng cũ (viết mới hoàn toàn — hiện chưa có test Keyence nào trong GrafcetStudio.App.Tests để làm baseline)
Risks
1. Siemens parser naming/structure too coupled
Mitigation: refactor sang namespace shared trước, không để Keyence phụ thuộc class tên Siemens.
2. Keyence mnemonic OR/grouping khó emit trực tiếp
Mitigation: thiết kế emitter theo strategy, cho phép thêm temporary/intermediate relay path sau này.
3. Output instructions mở rộng làm vỡ design ban đầu
Mitigation: tách riêng expression AST và output instruction model ngay từ đầu.
4. Backward compatibility với generator cũ
Mitigation: giữ nhánh fallback cho simple condition và test regression.
Suggested File/Module Targets
Shared
src/GrafcetStudio.App/Expressions/LogicExpression.cs
src/GrafcetStudio.App/Expressions/LogicExpressionParser.cs
src/GrafcetStudio.App/Expressions/LogicExpressionParseException.cs
src/GrafcetStudio.App/Expressions/LogicExpressionWalker.cs
(Tất cả phải nằm trong project src/GrafcetStudio.App/GrafcetStudio.App.csproj — KHÔNG dùng GrafectStudio.App.csproj, file trùng tên bị lỗi chính tả, không có ref Sprache và không nằm trong GrafectStudio.sln)
Keyence
src/GrafcetStudio.App/Generators/Keyence/KeyenceInstructionModel.cs
src/GrafcetStudio.App/Generators/Keyence/KeyenceMnemonicExpressionEmitter.cs
update:src/GrafcetStudio.App/Generators/KeyenceMnemonicGenerator.cs
possibly src/GrafcetStudio.App/Generators/KeyenceGenerator.cs

Siemens
update references in:src/GrafcetStudio.App/Generators/Siemens/SiemensLadDslGenerator.cs
any Siemens tests using parser/model directly

Delivery Strategy
Milestone 1
Shared parser extracted
Siemens switched to shared parser
No Keyence behavior change yet
Milestone 2
Keyence instruction abstraction completed
Keyence emitter prototype works for A, A&B, A|B
Milestone 3
Grouped expression support works for (A|B)&C
Integrated into Keyence generator
Regression tests passing
Milestone 4
Output model ready for future instruction expansion
Documentation / maintenance notes added
Effort Estimate
Small implementation
Shared parser refactor
Basic Keyence emitter
Basic tests
Estimated: 1–2 days
Practical production-ready implementation
Clean abstraction
Integration
Error handling
Test coverage
Future-extensible output model
Estimated: 2–5 days
Success Criteria
Hoàn thành khi đạt được các điều kiện sau:
Siemens và Keyence cùng dùng chung expression parser/model
Keyence parse được:A&B
A|B
(A|B)&C

Generator Keyence emit ra mnemonic hợp lệ cho single-output flow
Thiết kế output layer không khóa cứng vào OUT/SET/RST/FB
Có test coverage đủ để mở rộng về sau mà không phải viết lại parser