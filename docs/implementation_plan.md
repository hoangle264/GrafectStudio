# KeyenceGenerator Refactoring Implementation Plan

This plan details the step-by-step refactoring of the `KeyenceGenerator` class to reduce its size, decouple its components, and ensure correctness via baseline regression testing.

## User Review Required

> [!IMPORTANT]
> **Handlebars Property Casing**:
> The `TemplateManager` and Handlebars templates rely on property reflection. When converting anonymous objects to C# records, we MUST keep properties in camelCase or match the exact casing of properties currently outputted, otherwise templates will silently render empty blocks. We will test after each group conversion.

## Open Questions

None at this stage. We have validated that the test suite runs and passes successfully.

---

## Proposed Changes

We will perform the refactoring in discrete steps, running the test suite after each step to verify correctness.

### Phase 0: Baseline Snapshot Tests

We will create a regression test that runs a complete generator payload (incorporating linear flows, macro calls, and complex device outputs with interlocks/feedbacks) and records the output to a "golden file". Subsequent refactoring steps will compare their output against this golden file to ensure exactly 0 diff.

#### [NEW] [KeyenceGeneratorBaselineTests.cs](file:///c:/Users/NITRO%205/source/vscode/GrafectStudio/src/GrafcetStudio.App.Tests/KeyenceGeneratorBaselineTests.cs)
- Create a test class containing a comprehensive smoke/regression test.
- The test will run `KeyenceGenerator.GenerateUnitContent(payload)` and compare the string output against an embedded or local golden file `KeyenceGoldenOutput.mnm`.
- The test will write the generated output to a temporary/actual file if diffs are found, allowing easy debugging.

---

### Phase 1: Extract Pure Helpers

We will pull out helper groups into dedicated utility classes in the `GrafcetStudio.App.Generators.Keyence` namespace.

#### [NEW] [ExpressionHelper.cs](file:///c:/Users/NITRO%205/source/vscode/GrafectStudio/src/GrafcetStudio.App/Generators/Keyence/ExpressionHelper.cs)
- Move methods: `BuildConditionExpression`, `BuildActivationExpression`, `BuildHoldExpression`, `BuildDoneConditionExpression`, `NegateExpression`, `JoinAnd`, `JoinByAggregationMode`, `WrapConditionTerm`, `WrapCompoundExpression`, `AddConditionTerm`, `IsFalseState`.

#### [NEW] [MnemonicEmitter.cs](file:///c:/Users/NITRO%205/source/vscode/GrafectStudio/src/GrafcetStudio.App/Generators/Keyence/MnemonicEmitter.cs)
- Move methods: `EmitRung`, `EmitRungLines`, `EmitConditionLines`, `JoinMnemonicLines`, `JoinMnemonicBlocks`, `SplitMnemonicLines`, `ToInstruction`.

#### [NEW] [StepAddressHelper.cs](file:///c:/Users/NITRO%205/source/vscode/GrafectStudio/src/GrafcetStudio.App/Generators/Keyence/StepAddressHelper.cs)
- Move methods: `TryParseAddressBase`, `FormatAddressBase`, `ResolveBoolBase`, `ResolveBoolMr`, `FormatWordAddress`, `FormatWordStepExecAddress`, `ResolveSequenceEnd`, `IncrementParsedAddress`, `ResolveAddressSortValue`, `TryParseStepExecAddress`, `BuildFlowStepAddressRange`, `BuildUnitStepAddressRange`.
- Move inner record structs `ParsedBoolBase` and `StepExecAddress` to this helper file.

#### [MODIFY] [KeyenceGenerator.cs](file:///c:/Users/NITRO%205/source/vscode/GrafectStudio/src/GrafcetStudio.App/Generators/KeyenceGenerator.cs)
- Replace local helper implementations with calls to the extracted helper classes.

---

### Phase 2: Pipeline BuildContext & Factory

We will decouple the workflow context building process from the generator itself.

#### [NEW] [GeneratorContextFactory.cs](file:///c:/Users/NITRO%205/source/vscode/GrafectStudio/src/GrafcetStudio.App/Generators/Keyence/GeneratorContextFactory.cs)
- Create a context builder/factory class.
- Move methods: `BuildContext`, `BuildResolvedFlow`, `BuildFlowGroup`, `EnrichStepActions`, `EnrichStepAction`, `BuildSensorRef`, `LoadDeviceLibrary`, `NormalizeFlowType`, `NormalizeDiagramType`, `ResolveFlowUnitId`, `AnalyzeMacroFlows`, `ResolveAndValidateMacroPortVariable`, `ResolveMacroPortVariable`, `BuildMacroPortName`, `NormalizeDeviceKind`, `ResolveStandardDevicePartial`.
- Expose a single entry point: `GeneratorContextFactory.Create(payload, sequenceResolver)`.

#### [MODIFY] [KeyenceGenerator.cs](file:///c:/Users/NITRO%205/source/vscode/GrafectStudio/src/GrafcetStudio.App/Generators/KeyenceGenerator.cs)
- Reduce `BuildContext(payload)` to a single line invoking the factory.

---

### Phase 3: Extract DeviceOutputGroupBuilder

We will extract output aggregation and intent building logic.

#### [NEW] [DeviceOutputGroupBuilder.cs](file:///c:/Users/NITRO%205/source/vscode/GrafectStudio/src/GrafcetStudio.App/Generators/Keyence/DeviceOutputGroupBuilder.cs)
- Move methods: `BuildDeviceOutputGroups`, `FindUnitVariable`, `BuildCommandFlowOutputs`, `BuildInterlockExpression` (the output variant), `ResolveOutputInstruction`, `ResolveModeFlagAddress`, `TryGetAddress`, `MergeOutputBindings`.

#### [MODIFY] [GeneratorContextFactory.cs](file:///c:/Users/NITRO%205/source/vscode/GrafectStudio/src/GrafcetStudio.App/Generators/Keyence/GeneratorContextFactory.cs)
- Delegate output group generation to `DeviceOutputGroupBuilder`.

---

### Phase 4: Anonymous Objects to Records

We will replace anonymous objects in the context with formal C# records to improve typing, readability, and template safety.

#### [NEW] [GeneratorContextModels.cs](file:///c:/Users/NITRO%205/source/vscode/GrafectStudio/src/GrafcetStudio.App/Generators/Keyence/GeneratorContextModels.cs)
- Define records: `GeneratorContext`, `UnitContext`, `DeviceContext`, `DeviceSignalContext`, `FlowGroupContext`, `MacroBindingContext` (if not already fully structured), `MacroPortContext`.
- Ensure casing matches the expected camelCase property names in templates.

#### [MODIFY] [GeneratorContextFactory.cs](file:///c:/Users/NITRO%205/source/vscode/GrafectStudio/src/GrafcetStudio.App/Generators/Keyence/GeneratorContextFactory.cs)
- Map anonymous object instantiations to the new record types.

---

### Phase 5: LINQ Extensions & Cleanup

We will refactor repetitive LINQ patterns into extension methods.

#### [NEW] [LinqExtensions.cs](file:///c:/Users/NITRO%205/source/vscode/GrafectStudio/src/GrafcetStudio.App/Generators/Keyence/LinqExtensions.cs)
- Add utility extension methods: `DistinctIgnoreCase`, `Trimmed`, `NotEmpty`, `ToDictionaryIgnoreCase`.

#### [MODIFY] [GeneratorContextFactory.cs](file:///c:/Users/NITRO%205/source/vscode/GrafectStudio/src/GrafcetStudio.App/Generators/Keyence/GeneratorContextFactory.cs) and [DeviceOutputGroupBuilder.cs](file:///c:/Users/NITRO%205/source/vscode/GrafectStudio/src/GrafcetStudio.App/Generators/Keyence/DeviceOutputGroupBuilder.cs)
- Adopt new extensions to simplify code.

---

## Verification Plan

### Automated Tests
- Build code and run all tests: `dotnet test src/GrafcetStudio.App.Tests/GrafcetStudio.App.Tests.csproj`
- Ensure our newly added `KeyenceGeneratorBaselineTests` reports `0` diff against the golden file at every refactoring stage.

### Manual Verification
- Render a live template using existing test scripts if possible.
