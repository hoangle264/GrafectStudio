# Step-by-step Prompts for GrafcetStudio

## Cách dùng
- Chạy lần lượt từ Bước 1 → Bước 8.
- Mỗi bước chỉ thực hiện đúng phạm vi của bước đó.
- Luôn giữ nguyên rule: **chỉ tạo signature, không implement body** (trừ khi bạn yêu cầu khác).

## Bước 1 — Khởi tạo phạm vi và rule
```text
Bạn là senior .NET architect.
Hãy xác nhận phạm vi công việc cho GrafcetStudio:
- C# 12, .NET 10, WPF
- Prism 9 + DryIoc
- Handlebars.Net
- xUnit + Moq
- Chỉ tạo class/property/method signature, KHÔNG implement body
- SRP, constructor injection, không static mutable/global state
- Không hardcode hãng PLC hoặc loại thiết bị
- Magic string đưa vào constant/enum
Sau đó liệt kê ngắn gọn checklist sẽ tuân thủ trong toàn bộ lần sinh code.
```

## Bước 2 — Sinh Domain.Enums
```text
Hãy tạo đầy đủ enum trong namespace GrafcetStudio.Domain.Enums theo file-scoped namespace, mỗi enum ở file riêng:
- SignalVarType { Input, Output, Var }
- BranchType { Normal, ParallelSplit, ParallelJoin, AlternativeSplit }
- ActionQualifier { N, S, R, P, P0, L, D, SD, DS, SL }
- DiagnosticLevel { Info, Warning, Error }
Yêu cầu:
- Có XML doc 1 dòng cho từng enum
- Không thêm giá trị ngoài đặc tả
- Chỉ trả về code
```

## Bước 3 — Sinh Domain.Models
```text
Hãy tạo các model trong GrafcetStudio.Domain.Models đúng signature theo đặc tả:
MrPair, StepAction, Step, Transition, Connection, DiagramState, SequenceEntry,
DeviceSignal, DeviceType, DeviceVariable, SignalInfo, DiagramMeta.
Yêu cầu:
- File-scoped namespace
- Mỗi type một file
- XML doc 1 dòng/class hoặc record
- Property immutable dùng get; init; khi phù hợp
- Chỉ khai báo method signature, không body
- Chỉ trả về code
```

## Bước 4 — Sinh Resolution layer
```text
Hãy tạo GrafcetStudio.Domain.Resolution:
- ISequenceResolver
- SequenceResolver
- SignalResolver (static)
Đúng method signature theo đặc tả.
Ràng buộc:
- SequenceResolver implement ISequenceResolver
- SignalResolver có các method resolve/find/check literal
- Không implement body
- Chỉ trả về code
```

## Bước 5 — Sinh CodeGen core + Profile + Template
```text
Hãy tạo các type trong GrafcetStudio.CodeGen:
- Models: GenerationOptions, GenerationResult, DiagramResult, Diagnostic, SignalActionEntry, DiagramEntry
- Interfaces/classes: ICodeGenerator, IlGenerator, StGenerator, StStepRenderParams
- Profile: PlcProfile, ProfileRegistry
- Template: TemplateManager, TemplateEntry, TemplateHealth, TemplateHealthEntry
Yêu cầu:
- Constructor injection đúng ràng buộc (IlGenerator/StGenerator nhận IProjectRepository + ISequenceResolver; TemplateManager nhận IHandlebars)
- ProfileRegistry có built-in profile properties theo đặc tả
- Chỉ signature, không body
- Chỉ trả về code
```

## Bước 6 — Sinh Runtime layer
```text
Hãy tạo GrafcetStudio.CodeGen.Runtime gồm:
DeviceLibrary, DeviceTypeConfig, CommandDef, CompleteDef,
DeviceLibraryLoader, RuntimeResolverOptions, DeviceInstanceMeta,
RuntimeResolver, ActionResolveResult, ExecuteSignalResult,
FeedbackSignalResult, OutputBinding, RuntimePlanner,
RuntimePlanOptions, StepRuntimePlan, PlanValidation,
DiagramRuntimePlan, RuntimePreviewOptions,
OutputBindingPlanner, OutputBindingPlan, AggregatedOutputBinding.
Ràng buộc:
- DeviceLibraryLoader là static class thuần
- RuntimeResolver.FindCommand tra cứu bằng driveSignal name
- OutputBindingPlanner không phụ thuộc PlcProfile
- Chỉ signature, không body
- Chỉ trả về code
```

## Bước 7 — Sinh DI interfaces dùng chung
```text
Hãy tạo interface dùng cho DI:
- IProjectRepository
- ISequenceResolver (nếu chưa có thì tạo, nếu có thì đối chiếu đúng signature)
Yêu cầu:
- Namespace hợp lý theo kiến trúc
- Chỉ signature, không body
- Chỉ trả về code
```

## Bước 8 — Sinh test skeleton
```text
Hãy tạo skeleton test class trong GrafcetStudio.Tests:
- SequenceResolverTests
- SignalResolverTests
- IlGeneratorTests
- RuntimeResolverTests
- OutputBindingPlannerTests
Yêu cầu:
- xUnit + Moq
- Mỗi test class có 1 Setup/fixture method
- Tên test method theo pattern Should_[ExpectedBehavior]_When_[Condition]
- Tạo đúng các method tên theo đặc tả
- Chỉ khai báo test skeleton, không implement assert logic chi tiết
- Chỉ trả về code
```
