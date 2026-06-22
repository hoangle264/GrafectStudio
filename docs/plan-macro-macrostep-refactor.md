# Plan: Macro / MacroStep Flow Refactor

## 1. Goal

Add flow-to-flow call mechanism using **Macro Step**.

Meaning:

```txt
Macro
  -> independent/runnable flow inside one unit

MacroStep
  -> child flow body
  -> not runnable independently
  -> called by exactly one Macro Step in same unit
```

Do not use `Main` / `SubRoutine` naming.

---

## 2. Terms

### Flow type

```txt
diagramType = "Macro" | "MacroStep"
```

### Step type

```txt
step.kind = "normal" | "macro"
```

### Macro step reference

```txt
step.macroFlowId = "<id of MacroStep flow>"
```

---

## 3. Relationship rules

### Rule 1: Macro runs independently

Flow `Macro` is independent/runnable flow in unit.

One unit can have many `Macro` flows.

```txt
Unit A
  Flow Pick      -> Macro
  Flow Place     -> Macro
  Flow Home      -> Macro
```

`Macro` is not unique entry point like C/C++ `main`.

---

### Rule 2: MacroStep does not run independently

Flow `MacroStep` is body of macro step.

```txt
Flow ClampSequence -> MacroStep
```

It runs only when called by special step in `Macro` flow.

---

### Rule 3: 1-1 relation

One `MacroStep` flow is called by exactly one macro step.

```txt
Macro Step S10 -> calls FlowB
FlowB -> one caller only
```

Do not allow many macro steps to call same `MacroStep` flow.

Reason:

- Easier codegen
- Easier state management
- Easier debug
- No multi-instance/reentrant logic needed

---

### Rule 4: Same unit only

Macro step can call only `MacroStep` flow with same `unitId`.

Do not allow:

```txt
Unit A / Macro flow -> calls Unit B / MacroStep flow
```

Reason:

- Keep unit boundary clean
- Avoid cross-unit coupling
- SystemControl orchestrates units, not internal macro bodies

---

### Rule 5: No nested macro call

Flow `MacroStep` cannot contain step with `kind = "macro"`.

Allowed:

```txt
Macro
  -> MacroStep
```

Not allowed:

```txt
Macro
  -> MacroStep
       -> another MacroStep
```

Reason:

- Simpler codegen
- Simpler validation
- No call stack
- No recursion/cycle detection complexity
- Enough for first phase

---

## 4. UI changes

### Flow property

Add/change field:

```txt
Diagram Type:
  Macro
  MacroStep
```

UI meaning:

```txt
Macro
  Independent/runnable flow in unit

MacroStep
  Called only by Macro Step in same unit
```

---

### New palette tool: Macro Step

Add tool:

```txt
Macro Step
```

When user places it in graph:

```txt
step.kind = "macro"
step.macroFlowId = null
```

Step property panel:

```txt
Macro Flow:
  dropdown list of eligible MacroStep flows
```

Dropdown filters:

- `diagramType = "MacroStep"`
- same `unitId`
- not already bound by another macro step

---

## 5. Validation

Run before codegen.

### Error cases

#### Macro step has no target

```txt
Step S10 is macro step but macroFlowId is empty.
```

#### Target flow missing

```txt
Step S10 references missing MacroStep flow: FlowB.
```

#### Target flow wrong type

```txt
Step S10 references flow FlowB, but target diagramType is not MacroStep.
```

#### Target flow in another unit

```txt
Step S10 references MacroStep FlowB from another unit.
```

#### MacroStep referenced multiple times

```txt
MacroStep FlowB is referenced by multiple macro steps.
```

#### MacroStep contains nested macro step

```txt
MacroStep FlowB cannot contain nested macro steps.
```

### Warning cases

#### MacroStep has no caller

```txt
MacroStep FlowB is not referenced by any Macro Step.
```

Keep as warning so user can keep draft flows.

---

## 6. Model changes

### Flow

```txt
FlowInfo
  id
  name
  unitId
  diagramType: "Macro" | "MacroStep"
  category: "normal" | "orchestrator"
  controlState
  steps
  transitions
```

### Step

```txt
Step
  id
  number
  label
  kind: "normal" | "macro"
  macroFlowId?: string
  actions
```

---

## 7. Macro interface

Use shared interface object between caller macro step and callee MacroStep flow.

Chosen name:

```txt
MacroPort
```

Reason:

- Short
- Works like interface/port between caller and callee
- Easier to extend
- Not limited to handshake-only meaning
- Similar idea to `Unit Station` as boundary object

Rejected/alternative:

```txt
MacroHandshake
```

`MacroHandshake` is valid but narrower, focused only on protocol signals.

---

## 8. MacroPort structure

Concept:

```txt
MacroPort
  Enable
  Start
  Busy
  Done
  Error
  Reset
```

Meaning:

```txt
Enable
  Caller allows macro body to run

Start
  Caller starts cycle

Busy
  MacroStep is running

Done
  MacroStep completed

Error
  MacroStep failed

Reset
  Reset macro body state
```

Keep structure in template/profile so user can extend or shrink.

Possible future fields:

```txt
Abort
Paused
State
ErrorCode
Timeout
```

Core should rely only on standard minimum fields unless template profile defines more behavior.

---

## 9. Codegen concept

### Normal step

Render as current normal step logic.

```txt
IF S1_ACTIVE THEN
    ...
END_IF;
```

### Macro step

Render call to child flow through `MacroPort`.

Concept:

```txt
IF S10_ACTIVE THEN
    ClampSequence_Port.Enable := TRUE;
    ClampSequence_Port.Start := TRUE;

    IF ClampSequence_Port.Done THEN
        S10_DONE := TRUE;
    END_IF;

    IF ClampSequence_Port.Error THEN
        S10_ERROR := TRUE;
    END_IF;
ELSE
    ClampSequence_Port.Start := FALSE;
END_IF;
```

### MacroStep flow body

Child flow receives `MacroPort`.

Concept:

```txt
IF Port.Reset THEN
    // reset internal steps
END_IF;

IF Port.Enable AND Port.Start THEN
    Port.Busy := TRUE;
    // run internal grafcet
END_IF;

IF internal complete THEN
    Port.Done := TRUE;
    Port.Busy := FALSE;
END_IF;
```

---

## 10. Output files

Keep multi-file architecture.

Initial output:

```txt
Units/Unit_<unitName>.st
```

Inside unit file:

- render `Macro` flows
- render `MacroStep` flows
- declare `MacroPort` for each 1-1 binding

Future optional split:

```txt
Units/Unit_<unitName>.st
Units/Macros/<MacroStepName>.st
```

Initial phase should keep MacroStep body inside `Unit_<name>.st` for simpler export.

---

## 11. Template impact

Profile `simple` / `packml` can render Macro/MacroStep differently through `.hbs`.

Add render context fields:

```txt
macroFlows
macroStepFlows
macroBindings
macroPorts
```

Example `macroBindings`:

```json
[
  {
    "unitId": "unit1",
    "callerFlowId": "flowA",
    "callerStepId": "S10",
    "calleeFlowId": "flowB",
    "portName": "FlowB_Port"
  }
]
```

Template can use `macroBindings` to:

- declare ports
- render caller macro step logic
- render callee MacroStep body interface

---

## 12. Orchestrator relation

Keep separate:

```txt
category = "orchestrator"
```

Do not mix with Macro/MacroStep behavior.

Concept:

```txt
orchestrator
  -> controls units or independent Macro flows

Macro
  -> independent logic inside unit

MacroStep
  -> child logic, called only by Macro Step
```

SystemControl should not call `MacroStep` directly.

---

## 13. Migration

Old project default:

```txt
diagramType = "Macro"
```

Old steps default:

```txt
kind = "normal"
```

This keeps old projects working.

---

## 14. Implementation order

1. Update model:
   - `FlowInfo.DiagramType`
   - `Step.Kind`
   - `Step.MacroFlowId`

2. Add migration:
   - missing flow `diagramType` -> `Macro`
   - missing step `kind` -> `normal`

3. Update UI flow property:
   - dropdown `Macro | MacroStep`

4. Add UI palette tool:
   - `Macro Step`

5. Update step property panel:
   - if `kind = macro`, show target `MacroStep` flow dropdown

6. Update payload builder:
   - send `diagramType`, `kind`, `macroFlowId`

7. Add validation:
   - 1-1 relation
   - same unit only
   - no nested macro step inside MacroStep flow
   - target must be `MacroStep`

8. Add codegen resolver:
   - build `macroBindings`
   - build `macroPorts`

9. Update Unit generator:
   - render normal steps
   - render macro steps
   - render MacroStep bodies

10. Update templates:
    - support `MacroPort`
    - support macro step call section
    - support MacroStep body section

11. Add tests:
    - one Macro calls one MacroStep -> OK
    - MacroStep called twice -> error
    - MacroStep from another unit -> error
    - MacroStep contains macro step -> error
    - old project still generates

---

## 15. Final decisions

```txt
Flow diagramType:
  Macro
  MacroStep

Step kind:
  normal
  macro

Relationship:
  one MacroStep flow <-> one Macro Step only
  same unit only
  no nested MacroStep call

Interface:
  MacroPort
```

This design gives clear call point, simpler validation, simpler codegen, and keeps template profiles flexible.
