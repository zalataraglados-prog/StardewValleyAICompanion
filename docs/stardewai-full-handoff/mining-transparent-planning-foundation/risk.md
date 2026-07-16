# Risk

Current-status correction: combat, natural pickup, food recovery, ladder/shaft descent, exit, and after-snapshot rolling compilation are implemented. The ordinary-mine 96-to-98 loop is runtime verified. The older blocked-executor statements below describe the foundation slice, not current main.

## Risk Level

- Read-side risk is low after isolated E: validation of compact collision and non-empty object/monster rows.
- Historical foundation status: one-stone native execution was the only implemented action. Current main includes combat, descent, and retreat; ordinary multi-floor integration and one Skull Cavern shaft lifecycle are verified. The remaining high-risk boundaries are arbitrary-depth duration calibration and broader combat/loot combinations.

## Residual Risks

- Purpose-limited level 99 snapshots measured 127-164 ms and 232,759 bytes. Generic `locations` must remain excluded from `profile=mining`; including it previously produced about 2.63 MB and exceeded the 3000 ms gate.
- Private `BreakableContainer.health` and `MineShaft.netIsTreasureRoom` are read by exact reflected field name; a future game update must fail closed if either field changes.
- Monster future movement/attacks are not predicted. Current state is complete, and the eventual executor must re-read after every dynamic change.
- Route/collision details drive exact compact-grid BFS for every rolling step. Dynamic blockers now cause replanning, and native stone/ladder/exit transitions are verified across three ordinary-mine levels. Longer and more hostile runs may expose additional timing or combat interactions.
- Stone ladder preview is exact for the current save/day/floor/tile seed, but monster-drop ladder paths consume global runtime RNG and remain after-state observations.

## Mitigations

- Candidate generation fails closed when any required mining group or nested required fact is missing, stale, errored, or unavailable.
- Known impossible target depths and family mismatches are rejected before runtime.
- Shaft transparency, planning, and execution require Skull Cavern `mine_kind`, area `121`, and level `>120`; ordinary mines ignore malformed shaft rows, quarry mine remains sentinel `77377`, and `VolcanoDungeon` is excluded from the MineShaft chain.
- The compiler emits only a snapshot-bounded internal primitive and never guesses arbitrary-depth full-objective duration. Day planning fails closed until a duration model is calibrated from broader runtime samples.
- Focused mining tests, E: read/action smokes, and the ordinary 96-to-98 rolling loop pass. Internal executor primitives remain compiler-owned and cannot be selected by the small model.
