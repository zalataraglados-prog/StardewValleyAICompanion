# Risk

## Risk Level

- Read-side risk is low after isolated E: validation of compact collision and non-empty object/monster rows.
- One-stone native execution risk is low after isolated E: validation; full mining runtime risk remains high because combat, descent, retreat, and the multi-floor loop are not implemented.

## Residual Risks

- Purpose-limited level 99 snapshots measured 127-164 ms and 232,759 bytes. Generic `locations` must remain excluded from `profile=mining`; including it previously produced about 2.63 MB and exceeded the 3000 ms gate.
- Private `BreakableContainer.health` and `MineShaft.netIsTreasureRoom` are read by exact reflected field name; a future game update must fail closed if either field changes.
- Monster future movement/attacks are not predicted. Current state is complete, and the eventual executor must re-read after every dynamic change.
- Route/collision details drive exact compact-grid BFS for the first floor step. Native walking and pickaxe use are verified for one stone; dynamic monster navigation/combat is still absent.
- Stone ladder preview is exact for the current save/day/floor/tile seed, but monster-drop ladder paths consume global runtime RNG and remain after-state observations.

## Mitigations

- Candidate generation fails closed when any required mining group or nested required fact is missing, stale, errored, or unavailable.
- Known impossible target depths and family mismatches are rejected before runtime.
- Compiler always returns `mining_cost_estimate_unavailable` and `mining_perfect_executor_not_implemented`, keeps mining timing/energy unknown, and emits no fake low-level actions.
- Focused mining tests and E: read/action smokes pass. `mining.reach_depth` remains disabled until every semantic loop component is present; internal `executor.mine_stone` cannot be selected by the small model.
