# ABB RAPID JSON IR Patterns

## Schema Overview
JSON IR is the intermediate representation for robot code generation. AI generates ONLY JSON IR following the schema `grafectstudio/robot-ir/v1`.

---

## Pattern 1: Basic Pick and Place

### Description
Wait for part sensor, move to wait position, move linear to pick point, close gripper, move to wait position, move to place point, open gripper, return to home position.

### JSON IR
```json
{
  "$schema": "grafectstudio/robot-ir/v1",
  "platform": "ABB_RAPID",
  "module": "MainModule",
  "signals": [
    { "name": "gripper_open", "type": "DO", "alias": "DO1" },
    { "name": "sensor_part_present", "type": "DI", "alias": "DI1" }
  ],
  "positions": [
    { "name": "pHome", "motionType": "AbsJ", "speed": "Fast", "description": "Home position" },
    { "name": "pWait", "motionType": "Joint", "speed": "Medium", "description": "Safe intermediate" },
    { "name": "pPickup", "motionType": "Linear", "speed": "Precise", "description": "Pick point" },
    { "name": "pPlace", "motionType": "Linear", "speed": "Precise", "description": "Place point" }
  ],
  "tools": [
    { "name": "tool1", "description": "Gripper TCP" }
  ],
  "init": {
    "instructions": [
      { "type": "SetDO", "signal": "gripper_open", "value": 0 },
      { "type": "WaitTime", "seconds": 0.3 },
      { "type": "MoveAbsJ", "target": "pHome", "tool": "tool1" }
    ]
  },
  "steps": [
    {
      "id": 1,
      "name": "Wait for part",
      "grafectStepId": 1,
      "instructions": [
        { "type": "WaitDI", "signal": "sensor_part_present", "value": 1 }
      ]
    },
    {
      "id": 2,
      "name": "Move to pickup",
      "grafectStepId": 2,
      "instructions": [
        { "type": "MoveJ", "target": "pWait", "tool": "tool1" },
        { "type": "MoveL", "target": "pPickup", "tool": "tool1" }
      ]
    },
    {
      "id": 3,
      "name": "Grip part",
      "grafectStepId": 3,
      "instructions": [
        { "type": "SetDO", "signal": "gripper_open", "value": 1 },
        { "type": "WaitTime", "seconds": 0.5 }
      ]
    },
    {
      "id": 4,
      "name": "Place part",
      "grafectStepId": 4,
      "instructions": [
        { "type": "MoveL", "target": "pWait", "tool": "tool1" },
        { "type": "MoveJ", "target": "pPlace", "tool": "tool1" },
        { "type": "SetDO", "signal": "gripper_open", "value": 0 },
        { "type": "WaitTime", "seconds": 0.3 }
      ]
    },
    {
      "id": 5,
      "name": "Return home",
      "grafectStepId": 5,
      "instructions": [
        { "type": "MoveJ", "target": "pHome", "tool": "tool1" }
      ]
    }
  ],
  "flow": {
    "initFirst": true,
    "mainLoopSteps": [1, 2, 3, 4, 5],
    "loopBack": true
  }
}
```

---

## Pattern 2: Offset Motion (Approach & Clearance / Palletizing)

### Description
Move with Z-offset before picking, and calculate dynamic offsets for palletizing rows/columns.

### JSON IR Instruction Examples
```json
// Literal offset — approach 100mm along Z-axis above pick target
{
  "type": "MoveL",
  "target": "pPickup",
  "offset": { "x": 0, "y": 0, "z": 100 },
  "tool": "tool1"
}

// Expression offset — calculated offset based on pallet grid variables
{
  "type": "MoveL",
  "target": "pBasePallet",
  "offset": { "x": "nCol*fPitchX", "y": "nRow*fPitchY", "z": 0 },
  "tool": "tool1"
}
```
