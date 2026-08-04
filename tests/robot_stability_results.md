# Robot AI Codegen — Prompt Stability Test Results

**Date**: 2026-08-04  
**Target Platform**: ABB Robot (RAPID)  
**Schema Version**: `grafectstudio/robot-ir/v1`  
**Test Suite**: `RobotCodegenTests.cs`  

---

## 1. Test Scenarios & Results

| Scenario | Steps Count | Positions | Signals | Iterations | JSON IR Validity | Status |
|----------|-------------|-----------|---------|------------|------------------|--------|
| **1. Simple Pick & Place** | 5 steps | 2 POS (`pHome`, `pPickup`) | 2 Signals (`gripper_open`, `sensor_part_present`) | 5 / 5 | 100% Valid JSON IR | **PASS** |
| **2. Palletizing Loop** | 7 steps | 4 POS (`pHome`, `pWait`, `pPalletBase`, `pPlace`) | 4 Signals (`gripper_open`, `sensor_part`, `grid_full`, `conveyor_run`) | 5 / 5 | 100% Valid JSON IR (Supports `Offs()` matrix) | **PASS** |
| **3. Dual Station Transfer** | 10 steps | 6 POS (`pHome`, `pPickSt1`, `pWait1`, `pPickSt2`, `pWait2`, `pPlace`) | 6 Signals (DO/DI pairs) | 5 / 5 | 100% Valid JSON IR | **PASS** |

---

## 2. Summary & Pass Criteria

- **Pass Threshold**: $\ge$ 2/3 scenarios pass structural validation.
- **Actual Result**: 3/3 scenarios **PASS** (100% compliance with JSON IR Whitelist & Schema).
- **RAPID Rendering**: 100% deterministic code rendering without syntax errors.

---

*Verified by GrafectStudio Automated Test Suite (`dotnet test`)*
