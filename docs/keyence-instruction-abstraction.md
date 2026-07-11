# Keyence instruction abstraction

This note belongs to Phase 2 of `keyence-expression-support-plan.md`.

## Purpose

Keyence mnemonic generation now has an intermediate instruction model under
`GrafcetStudio.App.Generators.Keyence`.  The model separates two concerns:

1. condition/expression emission, which will produce the rung/logic leading up
   to an instruction; and
2. final instruction selection/formatting, which decides whether the output is
   `OUT`, `SET`, `RST`, `RES`, `FB`, or a future/custom mnemonic.

Keeping this seam explicit allows Phase 3/4 to add expression support without
hard-coding action qualifiers directly inside the expression emitter.

## Core model

- `KeyenceInstruction`
  - abstract base for every Keyence instruction abstraction.
  - carries optional `Comment` and a `Parameters` metadata bag for future
    instruction-specific arguments.
- `KeyenceInstructionType`
  - enum: `Out`, `Set`, `Rst`, `Res`, `Fb`, `Custom`.
- `KeyenceOutputInstruction`
  - base for single-target output-like instructions.
  - carries `TargetRef`, optional `SourceRef`, optional `Parameters`.
- Built-in output instruction records:
  - `KeyenceCoilInstruction` -> `OUT`
  - `KeyenceSetInstruction` -> `SET`
  - `KeyenceResetInstruction` -> `RST`
  - `KeyenceResetByResInstruction` -> `RES`
  - `KeyenceFunctionBlockInstruction` -> `FB`, with optional `BlockName` and
    `Body` placeholders.
- `KeyenceCustomInstruction`
  - escape hatch for future timer/move/call/custom mnemonic work.
- `KeyenceMnemonicInstructionEmitter`
  - formats instruction models into mnemonic text.

## Current mapping rules

Existing Grafcet action qualifiers keep the same behavior:

| Source | Instruction model | Mnemonic |
| --- | --- | --- |
| `ActionQualifier.N` | `KeyenceCoilInstruction` | `OUT` |
| `ActionQualifier.S` | `KeyenceSetInstruction` | `SET` |
| `ActionQualifier.R` | `KeyenceResetInstruction` | `RST` |

Additional instruction types defined for upcoming work:

| Instruction type | Model | Mnemonic |
| --- | --- | --- |
| `Res` | `KeyenceResetByResInstruction` | `RES` |
| `Fb` | `KeyenceFunctionBlockInstruction` | `FB` |
| `Custom` | `KeyenceCustomInstruction` | caller-supplied mnemonic |

Unsupported Grafcet action qualifiers are represented as
`KeyenceUnsupportedOutputInstruction` and still render as the old comment form,
for backward-compatible output.

## Future expansion

For timers, move/copy instructions, calls, or richer FB blocks, add a new record
that derives from `KeyenceInstruction` (or `KeyenceOutputInstruction` when it has
one target operand), then extend `KeyenceMnemonicInstructionEmitter`.  Expression
emitters should consume only `KeyenceInstruction`/`KeyenceOutputInstruction` and
should not know about `ActionQualifier`.
