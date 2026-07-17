# Grandpa Skull Key Acquisition Audit

## Status

Accepted on 2026-07-17.

- Model-facing objective: `mining.obtain_skull_key`.
- Candidate kind: `mining_obtain_skull_key_plan_envelope`.
- Valid family: ordinary mines, levels 1 through 120 only.
- Mandatory terminal interaction: floor-120 overlay reward chest containing `SpecialItem(4)`.
- Completion postcondition: transparent `player.has_skull_key=true`.
- Skull Cavern, Quarry Mine `77377`, and Volcano Dungeon are rejected.

## Runtime Proof

Primary artifact: `artifacts/runtime-mining-reach-depth/runtime-skull-key-20260717102345/summary.json`.

1. Floor 119: two `executor.mine_stone` steps and one `executor.break_container` step opened the live route.
2. `executor.descend_ladder`: changed the transparent depth from 119 to 120.
3. `executor.move_to_tile`: approached the transparent reward chest.
4. `executor.interact`: native open/claim sequence; observed `has_skull_key false -> true`.
5. `executor.exit_mine`: used the floor-120 two-tile exit stand and native ExitMine dialogue.

All seven primitives were applied and verified. Every after snapshot was fresh, every state hash changed, and seven executor-calibration rows were written. The isolated fixture reset is debug-only; the production executor never writes `hasSkullKey` directly.

The narrower floor-120 proof remains at `artifacts/runtime-mining-reach-depth/runtime-skull-key-20260717-180537/summary.json`.

## Container Calibration Closure

The original 119-to-120 failure is closed as RISK-009. `artifacts/runtime-mining-snapshot-smoke/runtime-container-lifecycle-20260717102239/summary.json` deterministically placed a live health-3 barrel, selected the native club `(W)63`, observed `3 -> 0` over one completed attack animation, and confirmed removal in the refreshed transparent snapshot. The primary 119-to-120 artifact then exercised a naturally selected container in the complete objective chain.
