# Siemens LAD JSON DSL Reference

This document describes the JSON DSL used by the GrafcetStudio `siemens-lad` generator. The default template is `templates/siemens-lad/default.lad.json`, and the editor/schema reference is `docs/schemas/siemens-lad-template.schema.json`.

## Template shape

```json
{
  "$schema": "docs/schemas/siemens-lad-template.schema.json",
  "version": "1.0",
  "platform": "siemens-lad",
  "tiaVersion": 17,
  "blockName": "{{unit.name}}_Grafcet_LAD",
  "blockNumber": 1,
  "parameters": [],
  "networks": []
}
```

`platform` must be `siemens-lad`. `blockName`, network `title`, and network `comment` support these placeholders:

- `{{project.name}}`
- `{{unit.name}}`
- `{{flow.name}}`
- `{{step.number}}`
- `{{step.label}}`
- `{{transition.label}}`
- `{{action.variable}}`

## Parameters

`parameters[]` declares operands that expression `TAG` nodes and output coils can reference by `key`.

```json
{
  "key": "exec",
  "source": "step.execAddress",
  "default": "Exec",
  "scope": "global",
  "dataType": "Boolean"
}
```

Supported source values currently include:

- `step.execAddress`
- `step.doneAddress`
- `transition.condition`
- `action.address`
- `action.variable`

If `source` is empty, unknown, or cannot resolve in the current repeat context, the generator falls back to `default`, then to `key` or the custom source text depending on the source form. `scope: "global"` renders a global variable access. `input`, `output`, `inout`, `temp`, and `constant` are added to the FC interface. `local` or an unknown scope renders a local variable access.

## Networks and repeat mode

Each network has an `id`, an `expression`, and an `output`.

```json
{
  "id": "step_activation",
  "repeat": "steps",
  "title": "Activate {{flow.name}} S{{step.number}}",
  "comment": "prevDone & condition -> exec",
  "expression": { "type": "TAG", "ref": "prevDone" },
  "output": { "type": "coil", "ref": "exec" }
}
```

`repeat` controls how often a network is rendered:

- `once`: render one network for the payload.
- `steps`: render one network for each step.
- `transitions`: render one network for each transition.
- `actions`: render one network for each step action.

## Expression tree

Expressions are recursive objects. `TAG` references a parameter key, `AND` connects child nodes in series, `OR` creates branches, and `NOT` negates a single `TAG` node.

### TAG simple

```json
{
  "expression": { "type": "TAG", "ref": "exec" },
  "output": { "type": "coil", "ref": "done" }
}
```

### Nested AND/OR

```json
{
  "expression": {
    "type": "AND",
    "nodes": [
      { "type": "TAG", "ref": "prevDone" },
      {
        "type": "OR",
        "nodes": [
          { "type": "TAG", "ref": "condition" },
          { "type": "TAG", "ref": "feedback" }
        ]
      }
    ]
  },
  "output": { "type": "coil", "ref": "exec" }
}
```

### NOT contact

```json
{
  "expression": {
    "type": "NOT",
    "node": { "type": "TAG", "ref": "feedback" }
  },
  "output": { "type": "coil", "ref": "exec" }
}
```

Runtime validation currently supports `NOT` around `TAG` only. For more complex negation, create a separate parameter or rewrite the logic with explicit positive contacts.

## Output coil types

The output object writes the result of the expression to a parameter operand.

```json
{ "type": "coil", "ref": "exec" }
```

Supported output types:

- `coil`: normal coil.
- `set_coil`: set/latch coil.
- `reset_coil`: reset/unlatch coil.

### Set/reset coil example

```json
{
  "parameters": [
    { "key": "start", "source": "transition.condition", "default": "StartCmd", "scope": "global", "dataType": "Boolean" },
    { "key": "stop", "source": "", "default": "StopCmd", "scope": "global", "dataType": "Boolean" },
    { "key": "run", "source": "step.execAddress", "default": "RunState", "scope": "global", "dataType": "Boolean" }
  ],
  "networks": [
    {
      "id": "set_run",
      "expression": { "type": "TAG", "ref": "start" },
      "output": { "type": "set_coil", "ref": "run" }
    },
    {
      "id": "reset_run",
      "expression": { "type": "TAG", "ref": "stop" },
      "output": { "type": "reset_coil", "ref": "run" }
    }
  ]
}
```

## Custom parameter source

Custom `source` values are allowed for forward-compatible templates. If the generator does not recognize the source prefix, it uses `default` when present; otherwise it uses the source text as the operand.

```json
{
  "parameters": [
    {
      "key": "operatorEnable",
      "source": "custom.operatorEnableTag",
      "default": "Operator_Enable",
      "scope": "global",
      "dataType": "Boolean"
    }
  ],
  "networks": [
    {
      "id": "operator_enable",
      "repeat": "once",
      "title": "Operator enable",
      "expression": { "type": "TAG", "ref": "operatorEnable" },
      "output": { "type": "coil", "ref": "operatorEnable" }
    }
  ]
}
```

## Validation notes

The generator validates templates at load time and reports logical JSON paths such as `networks[0].expression.nodes[1].ref`.

- `parameters[].key` must be non-empty and unique.
- `networks[].id`, `expression`, and `output` are required.
- `TAG.ref` and `output.ref` must reference declared parameter keys.
- `AND.nodes` and `OR.nodes` must contain at least one child.
- `NOT.node` is required and must be a `TAG` node.
- No runtime JSON schema validator dependency is required yet; the schema is for documentation and editor tooling.
