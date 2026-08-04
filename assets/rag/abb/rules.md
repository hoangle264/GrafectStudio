# ABB RAPID Generation Rules

1. **Deterministic Separation**:
   - AI outputs ONLY JSON IR schema (`grafectstudio/robot-ir/v1`).
   - Do NOT output RAPID syntax text directly in the AI response.

2. **Whitelist Instructions**:
   - `MoveJ`: Joint move to position. Fields: `target`, `tool`, optional `offset`.
   - `MoveL`: Linear move to position. Fields: `target`, `tool`, optional `offset`.
   - `MoveAbsJ`: Absolute joint move to home/safe point. Fields: `target`, `tool`.
   - `SetDO`: Set Digital Output. Fields: `signal`, `value` (0 or 1).
   - `WaitDI`: Wait for Digital Input state. Fields: `signal`, `value` (0 or 1).
   - `WaitTime`: Delay execution in seconds. Fields: `seconds`.
   - `SetAO`: Set Analog Output. Fields: `signal`, `value`.
   - `WaitAI`: Wait for Analog Input state. Fields: `signal`, `value`.

3. **Symbol & Declaration Safety Rules**:
   - All `target` positions must exist in the `positions` list and in the user's declared POS variables.
   - All `signal` names must exist in the `signals` list and in the user's declared DO/DI variables.
   - Every `SetDO` instruction should be followed by a short `WaitTime` (e.g., 0.3s) to allow pneumatic/mechanical activation.
   - The `init` block must reset all DO signals to 0 and execute `MoveAbsJ` to a safe home position.
