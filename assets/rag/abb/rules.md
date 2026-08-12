# ABB RAPID Hard Rules
- Speed data parameters: `v10`, `v50`, `v100`, `v500`, `v1000`, `vmax`.
- Zone data parameters: `fine`, `z0`, `z1`, `z5`, `z10`, `z50`, `z100`.
- Tool data: default `tool0`.
- Digital I/O signals: Use `SetDO signalName, 1;`, `SetDO signalName, 0;`, `WaitDI signalName, 1;`.
- Analog I/O signals: Use `SetAO signalName, val;`, `WaitAI signalName, val;`.
- Timers & Delays: Use `WaitTime 1.0;`.
- Emergency Stop / Reset: Ensure signal reset in error handlers.
