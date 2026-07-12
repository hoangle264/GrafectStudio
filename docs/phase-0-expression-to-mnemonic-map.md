# Phase 0 — Expression → Keyence mnemonic field map

Deliverable for Phase 0 in `docs/plan-expression-to-keyence-mnemonic.md`.

## Current pseudo-expression sources

- Step context is built in `src/GrafcetStudio.App/Generators/KeyenceGenerator.cs`:
  - `BuildStepExpressionContext`
  - `BuildStepActionExpressions`
  - `BuildStepOutputExpressions`
- Device output context is built in `BuildDeviceOutputGroups`.
- Pseudo rung format is produced by `BuildInstructionExpression(condition, instruction, target)`:
  - `condition -> INSTR target`
  - joined rungs use ` ; `.
- Existing mnemonic emitters are available but not wired into `KeyenceGenerator` yet:
  - `Generators/Keyence/KeyenceInstructionModel.cs`
  - `Generators/Keyence/KeyenceMnemonicExpressionEmitter.cs`
  - `Generators/Keyence/KeyenceMnemonicInstructionEmitter.cs`

## Template inventory

| Template | Current expression usage | Phase impact |
|---|---|---|
| `templates/auto.hbs` (`uc.auto`) | primary user of `expression.*` pseudo fields | Phase 1 + Phase 3 |
| `templates/main-output.hbs` (`uc.mainOutput`) | renders `deviceOutputGroups` through `renderDevice` | Phase 2 + Phase 3 |
| `templates/devices/*.hbs` | mostly hardcoded `LD/AND/ANB/...` from command fields, not `*.expression` | Phase 2 optional |
| `templates/origin.hbs` | hardcoded mnemonic from step/action addresses, not `expression.*` | later optional |
| `templates/manual.hbs` | hardcoded mnemonic/manual bits | skip |
| `templates/error.hbs`, `templates/output.hbs`, `templates/step-body.hbs` | no relevant expression output | skip |

`templates/auto.hbs` currently reads:

| HBS field | Current shape |
|---|---|
| `expression.activationExpression` | condition-only string |
| `expression.outputs[].expression` | pseudo rung |
| `expression.doneExpression` | pseudo rung |
| `expression.outTransition.expression` | condition-only comment |
| `expression.bodyExpression` | joined pseudo summary |
| `expression.hasOutputs`, `expression.hasTransition` | booleans |

## Chosen contract

Use a dual-field transition:

1. Keep every existing `*Expression` field for template compatibility.
2. Add mnemonic sibling fields in Phase 1/2.
3. C# emits mnemonic; HBS only prints mnemonic fields and must not parse `&`, `|`, `!`.
4. Use both block and line-list forms:
   - `xxxMnemonic`: newline-joined block string.
   - `xxxMnemonicLines`: `IList<string>` for `{{#each}}` templates.
5. Keep `templateContract.stepBody = "expression"` until Phase 3. Change it to `"mnemonic"` only when templates migrate.

## Expression field → mnemonic field map

### StepExpressionContext

| Existing field | Meaning | New field(s) |
|---|---|---|
| `conditionExpression` | alias of activation condition | `conditionMnemonic`, `conditionMnemonicLines` |
| `activationExpression` | activate condition | `activationMnemonic`, `activationMnemonicLines` |
| `holdExpression` | hold condition (`exec & !out`) | `holdMnemonic`, `holdMnemonicLines` |
| `doneExpression` | done pseudo rung | `doneMnemonic`, `doneMnemonicLines` |
| `doneConditionExpression` | done condition input | optional `doneConditionMnemonicLines` |
| `outputExpression` | joined output pseudo rungs | `outputMnemonic`, `outputMnemonicLines` |
| `bodyExpression` | joined body pseudo rungs | `bodyMnemonic`, `bodyMnemonicLines` |
| `bodyExpressions` | pseudo rung list | `bodyMnemonics` |
| `inTransitionExpression`, `outTransitionExpression` | condition-only transition comments | no instruction mnemonic in Phase 1 |

### StepActionExpressionContext

| Existing field | Meaning | New field(s) |
|---|---|---|
| `conditionExpression` + `instruction` + `target` | action rung inputs | `mnemonic`, `mnemonicLines` |
| `expression` | action pseudo rung | `mnemonic`, `mnemonicLines` |
| `completionExpression` | feedback condition only | no output mnemonic by itself |

### StepOutputExpressionContext

| Existing field | Meaning | New field(s) |
|---|---|---|
| `conditionExpression` + `instruction` + `target` | direct/output-binding rung inputs | `mnemonic`, `mnemonicLines` |
| `expression` | output pseudo rung | `mnemonic`, `mnemonicLines` |
| `interlockExpression` | condition-only input | no standalone output mnemonic |

### Device output models

| Existing object/field | Meaning | New field(s) |
|---|---|---|
| `DeviceCommandFlowOutput.conditionExpression` | per-flow source condition | optional `conditionMnemonic`, `conditionMnemonicLines` |
| `DeviceOutputIntent.expression` | device output pseudo rung | `mnemonic`, `mnemonicLines` |
| `DeviceCommandOutput.expression` | device command pseudo rung | `mnemonic`, `mnemonicLines` |
| `driveConditionExpression` | final condition input | input to device `mnemonic` only |

## Phase 1 helper contract

Suggested helper in `KeyenceGenerator` or adjacent renderer:

```csharp
KeyenceInstruction ToInstruction(string instruction, string target);
IReadOnlyList<string> EmitRungLines(string condition, string instruction, string target, IList<DeviceVariable> vars);
string EmitRung(string condition, string instruction, string target, IList<DeviceVariable> vars);
```

Instruction mapping:

| String | Model |
|---|---|
| `OUT` | `KeyenceInstructionType.Out` |
| `SET` | `KeyenceInstructionType.Set` |
| `RST` | `KeyenceInstructionType.Rst` |
| `RES` | `KeyenceInstructionType.Res` |
| other | unsupported/comment path |

Phase 1 rung inputs:

| Rung | Condition | Instruction | Target |
|---|---|---|---|
| Activate | `activationExpression` | `SET` | `step.ExecAddress` |
| Done | `doneConditionExpression` | `SET` | `step.DoneAddress` |
| Action | `action.conditionExpression` | `action.instruction` | `action.target` |
| Direct output | `output.conditionExpression` | `output.instruction` | `output.target` |

## Notes for Phase 3 template migration

When migrating HBS:

- `expression.outputs[].expression` → `expression.outputs[].mnemonic` or `mnemonicLines`.
- `expression.doneExpression` → `expression.doneMnemonic`.
- `expression.bodyExpression` → `expression.bodyMnemonic`.
- Update `templateContract` to version 2:
  - `name = "keyence-step-mnemonic"`
  - `stepBody = "mnemonic"`
  - add `stepMnemonicPath = "flow.steps[].expression.*Mnemonic"`.
