# Risk

## Risk Level

- Medium until an isolated E: snapshot confirms the new compact collision and per-object rows.
- Runtime risk remains high by design because the perfect mining/combat executor is explicitly not implemented.

## Residual Risks

- `MiningReadAdapter` compiles and its focused tests pass, but collision-grid cost and serialized shape still need isolated runtime measurement.
- Private `BreakableContainer.health` and `MineShaft.netIsTreasureRoom` are read by exact reflected field name; a future game update must fail closed if either field changes.
- Monster future movement/attacks are not predicted. Current state is complete, and the eventual executor must re-read after every dynamic change.
- Route/collision details are exposed as context for planning, but this slice does not implement a native mine navigation/combat executor.
- Stone ladder preview is exact for the current save/day/floor/tile seed, but monster-drop ladder paths consume global runtime RNG and remain after-state observations.

## Mitigations

- Candidate generation fails closed when any required mining group or nested required fact is missing, stale, errored, or unavailable.
- Known impossible target depths and family mismatches are rejected before runtime.
- Compiler always returns `mining_cost_estimate_unavailable` and `mining_perfect_executor_not_implemented`, keeps mining timing/energy unknown, and emits no fake low-level actions.
- Focused mining tests pass 25/25; runtime remains disabled until the E: read-side smoke and perfect executor exist.
