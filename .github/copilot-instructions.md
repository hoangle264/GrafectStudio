# Copilot Instructions — GrafcetStudio

## Mục tiêu
Sinh scaffold C# 12/.NET 10/WPF theo Prism 9 + DryIoc, kiến trúc tổng quát cho nhiều loại thiết bị/PLC.

## Phạm vi sinh code
- Chỉ tạo **class/property/method signature**.
- **Không implement method body**.
- Mỗi namespace một file `.cs`, dùng **file-scoped namespace**.
- Mỗi class có **XML doc 1 dòng** mô tả vai trò.
- Ưu tiên immutable: `property { get; init; }` khi phù hợp.

## Nguyên tắc thiết kế
- Tuân thủ **SRP**: mỗi class 1 trách nhiệm.
- Dependency phải rõ ràng, **constructor injection**; không global/static mutable state.
- Không hardcode hãng PLC hay loại thiết bị.
- Magic string phải đưa vào **constant/enum**.
- Method signature đầy đủ; thêm generic constraint nếu cần.

## Ràng buộc bắt buộc
1. Khác biệt PLC chỉ nằm ở `PlcProfile.InstructionMap`/`TimerInstruction`.
2. Hành vi thiết bị nằm ở `DeviceTypeConfig.Commands`.
3. `RuntimeResolver.FindCommand()` tra cứu theo **driveSignal name**.
4. `OutputBindingPlanner` chỉ gom LD/OR logic, không biết `PlcProfile`.
5. `IlGenerator` và `StGenerator` nhận `IProjectRepository`, `ISequenceResolver` qua constructor.
6. `TemplateManager` nhận `IHandlebars` qua constructor.
7. `DeviceLibraryLoader` là static class thuần, không state/DI.
8. Builtin helpers đăng ký trong `TemplateManager.RegisterBuiltinHelpers()`: `pad`, `eq`, `padStart2`.

## Domain/CodeGen/Test contract
- Giữ đúng contract model/enum/interface/method signature theo đặc tả dự án hiện tại.
- Các test class tạo skeleton theo pattern tên: `Should_[ExpectedBehavior]_When_[Condition]`.

## Công nghệ chuẩn
- C# 12, .NET 10, WPF
- Prism 9 + DryIoc
- Handlebars.Net
- Newtonsoft.Json hoặc System.Text.Json
- xUnit + Moq
